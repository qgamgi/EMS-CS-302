using EMS.API.DTOs;
using EMS.API.Hubs;
using EMS.API.Models;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace EMS.API.Services;

public interface IDispatchService
{
    Task<List<DispatchSummaryDto>> GetAllAsync();
    Task<DispatchDetailDto?> GetByIdAsync(string id);
    Task<DispatchDetailDto?> GetActiveForDriverAsync(string userId);
    Task<DispatchDetailDto> CreateAsync(CreateDispatchRequest request, string callerId);
    Task<DispatchDetailDto?> UpdateStatusAsync(string id, string status, string? cancellationReason = null);
    Task<DispatchDetailDto?> AssignDriverAsync(string id, string driverId);
}

public class DispatchService : IDispatchService
{
    private readonly IMongoCollection<Dispatch> _dispatches;
    private readonly IMongoCollection<Driver> _drivers;
    private readonly IMlService _mlService;
    private readonly IHubContext<DispatchHub> _hub;

    public DispatchService(
        IMongoDatabase db,
        IMlService mlService,
        IHubContext<DispatchHub> hub)
    {
        _dispatches = db.GetCollection<Dispatch>("dispatches");
        _drivers = db.GetCollection<Driver>("drivers");
        _mlService = mlService;
        _hub = hub;
    }

    public async Task<List<DispatchSummaryDto>> GetAllAsync()
    {
        var dispatches = await _dispatches
            .Find(_ => true)
            .SortByDescending(d => d.CreatedAt)
            .ToListAsync();

        return dispatches.Select(ToSummary).ToList();
    }

    public async Task<DispatchDetailDto?> GetByIdAsync(string id)
    {
        var dispatch = await _dispatches.Find(d => d.Id == id).FirstOrDefaultAsync();
        return dispatch == null ? null : ToDetail(dispatch);
    }

    public async Task<DispatchDetailDto?> GetActiveForDriverAsync(string userId)
    {
        var activeStatuses = new[]
        {
            DispatchStatus.Assigned,
            DispatchStatus.EnRoute,
            DispatchStatus.OnScene,
            DispatchStatus.Transporting,
        };

        var dispatch = await _dispatches
            .Find(d => d.AssignedDriverId == userId && activeStatuses.Contains(d.Status))
            .SortByDescending(d => d.UpdatedAt)
            .FirstOrDefaultAsync();

        return dispatch == null ? null : ToDetail(dispatch);
    }

    public async Task<DispatchDetailDto> CreateAsync(CreateDispatchRequest request, string callerId)
    {
        // Call ML service for hospital prediction
        MlPredictionResult? mlResult = null;
        try
        {
            mlResult = await _mlService.PredictAsync(
                request.Latitude, request.Longitude,
                request.Severity, request.Condition);
        }
        catch
        {
            // ML failure is non-fatal — dispatch is still created without prediction
        }

        var dispatch = new Dispatch
        {
            CallerId = callerId,
            PatientName = request.PatientName,
            Location = new PatientLocation
            {
                Lat = request.Latitude,
                Lng = request.Longitude,
                Address = request.Address,
            },
            Severity = request.Severity,
            Condition = request.Condition,
            NumberOfAmbulances = request.NumberOfAmbulances,
            Status = DispatchStatus.Pending,
            MlPrediction = mlResult == null ? null : new MlPrediction
            {
                HospitalId = mlResult.HospitalId,
                HospitalName = mlResult.HospitalName,
                HospitalLevel = mlResult.HospitalLevel,
                HospitalCoords = mlResult.HospitalCoords == null ? null
                    : new GeoPoint { Lat = mlResult.HospitalCoords.Lat, Lng = mlResult.HospitalCoords.Lng },
                DistanceKm = mlResult.DistanceKm,
                TimeComponents = mlResult.TimeComponents == null ? null
                    : new Models.TimeComponents
                    {
                        DispatchTime = mlResult.TimeComponents.DispatchTime,
                        TimeToPatient = mlResult.TimeComponents.TimeToPatient,
                        OnSceneTime = mlResult.TimeComponents.OnSceneTime,
                        TimeToHospital = mlResult.TimeComponents.TimeToHospital,
                        HandoverTime = mlResult.TimeComponents.HandoverTime,
                        TotalTime = mlResult.TimeComponents.TotalTime,
                    },
                EmsBaseId = mlResult.EmsBase?.BaseId ?? 0,
                EmsBaseName = mlResult.EmsBase?.BaseName ?? string.Empty,
                IsFallbackCalculation = mlResult.IsFallbackCalculation,
            },
            AssignedEmsBaseId = mlResult?.EmsBase?.BaseId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _dispatches.InsertOneAsync(dispatch);

        var detail = ToDetail(dispatch);

        // Broadcast to Dispatcher + EmsOperator groups
        await _hub.Clients.Groups("Dispatcher", "EmsOperator")
            .SendAsync("DispatchCreated", detail);

        return detail;
    }

    public async Task<DispatchDetailDto?> UpdateStatusAsync(string id, string status, string? cancellationReason = null)
    {
        if (!Enum.TryParse<DispatchStatus>(status, out var parsed))
            throw new ArgumentException($"Invalid status: {status}");

        var update = Builders<Dispatch>.Update
            .Set(d => d.Status, parsed)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        if (parsed == DispatchStatus.Completed)
            update = update.Set(d => d.CompletedAt, DateTime.UtcNow);

        if (parsed == DispatchStatus.Cancelled && cancellationReason != null)
            update = update.Set(d => d.CancellationReason, cancellationReason);

        await _dispatches.UpdateOneAsync(d => d.Id == id, update);

        var dispatch = await _dispatches.Find(d => d.Id == id).FirstOrDefaultAsync();
        if (dispatch == null) return null;

        // When the dispatch is completed or cancelled, reset the assigned
        // driver back to Available so they can receive new dispatches.
        if ((parsed == DispatchStatus.Completed || parsed == DispatchStatus.Cancelled)
            && dispatch.AssignedDriverId != null)
        {
            var driverUpdate = Builders<Driver>.Update
                .Set(dr => dr.Status, DriverStatus.Available)
                .Set(dr => dr.ActiveDispatchId, (string?)null)
                .Set(dr => dr.UpdatedAt, DateTime.UtcNow);

            // AssignedDriverId holds the User's ObjectId (see AssignDriverAsync)
            await _drivers.UpdateOneAsync(
                dr => dr.UserId == dispatch.AssignedDriverId, driverUpdate);
        }

        var detail = ToDetail(dispatch);
        await _hub.Clients.All.SendAsync("DispatchStatusUpdated", detail);
        return detail;
    }

    public async Task<DispatchDetailDto?> AssignDriverAsync(string id, string driverId)
    {
        // driverId is the Driver document _id (from the drivers list in the UI).
        // Resolve the Driver to get the associated UserId so the dispatch stores
        // the User's ObjectId — matching what the Driver JWT claim contains.
        var driver = await _drivers.Find(dr => dr.Id == driverId).FirstOrDefaultAsync();
        if (driver == null) return null;

        var userIdToStore = driver.UserId; // User's ObjectId

        var update = Builders<Dispatch>.Update
            .Set(d => d.AssignedDriverId, userIdToStore)
            .Set(d => d.Status, DispatchStatus.Assigned)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        await _dispatches.UpdateOneAsync(d => d.Id == id, update);

        // Mark driver as busy (filter by Driver._id)
        var driverUpdate = Builders<Driver>.Update
            .Set(dr => dr.Status, DriverStatus.Busy)
            .Set(dr => dr.ActiveDispatchId, id)
            .Set(dr => dr.UpdatedAt, DateTime.UtcNow);
        await _drivers.UpdateOneAsync(dr => dr.Id == driverId, driverUpdate);

        var dispatch = await _dispatches.Find(d => d.Id == id).FirstOrDefaultAsync();
        if (dispatch == null) return null;

        var detail = ToDetail(dispatch);

        // Notify the assigned driver by their UserId group
        await _hub.Clients.Group($"Driver_{userIdToStore}")
            .SendAsync("DriverAssigned", detail);
        await _hub.Clients.Groups("Dispatcher", "EmsOperator")
            .SendAsync("DispatchStatusUpdated", detail);

        return detail;
    }

    // ─── Mapping helpers ────────────────���──────────────────────────────────────

    private static DispatchSummaryDto ToSummary(Dispatch d) => new(
        d.Id!,
        d.PatientName,
        d.Severity,
        d.Condition,
        d.Status.ToString(),
        d.AssignedDriverId,
        d.MlPrediction?.HospitalName,
        d.MlPrediction?.TimeComponents?.TotalTime,
        d.CreatedAt
    );

    private static DispatchDetailDto ToDetail(Dispatch d) => new(
        d.Id!,
        d.PatientName,
        new LocationDto(d.Location.Lat, d.Location.Lng, d.Location.Address),
        d.Severity,
        d.Condition,
        d.Status.ToString(),
        d.AssignedDriverId,
        d.MlPrediction == null ? null : new MlPredictionDto(
            d.MlPrediction.HospitalId,
            d.MlPrediction.HospitalName,
            d.MlPrediction.HospitalLevel,
            d.MlPrediction.HospitalCoords == null ? null
                : new GeoPointDto(d.MlPrediction.HospitalCoords.Lat, d.MlPrediction.HospitalCoords.Lng),
            d.MlPrediction.DistanceKm,
            d.MlPrediction.TimeComponents == null ? null
                : new TimeComponentsDto(
                    d.MlPrediction.TimeComponents.DispatchTime,
                    d.MlPrediction.TimeComponents.TimeToPatient,
                    d.MlPrediction.TimeComponents.OnSceneTime,
                    d.MlPrediction.TimeComponents.TimeToHospital,
                    d.MlPrediction.TimeComponents.HandoverTime,
                    d.MlPrediction.TimeComponents.TotalTime),
            d.MlPrediction.EmsBaseId,
            d.MlPrediction.EmsBaseName,
            d.MlPrediction.IsFallbackCalculation
        ),
        d.CreatedAt,
        d.UpdatedAt,
        d.CompletedAt,
        d.CancellationReason,
        d.NumberOfAmbulances
    );
}

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
    /// <summary>
    /// Records that one driver (identified by their userId) has finished their
    /// part of the dispatch.  The dispatch only moves to Completed once every
    /// assigned driver has called this.
    /// </summary>
    Task<(DispatchDetailDto? dto, bool allDone)> CompleteDriverAsync(string dispatchId, string driverUserId);
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

        // Match on primary field OR the multi-driver list so all assigned drivers see it
        var dispatch = await _dispatches
            .Find(d => (d.AssignedDriverId == userId || d.AssignedDriverIds.Contains(userId))
                       && activeStatuses.Contains(d.Status))
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
            PatientAge = request.PatientAge,
            Location = new PatientLocation
            {
                Lat = request.Latitude,
                Lng = request.Longitude,
                Address = request.Address,
            },
            Severity = request.Severity,
            Condition = request.Condition,
            NumberOfAmbulances = request.NumberOfAmbulances,
            Paramedics = request.Paramedics ?? new List<string>(),
            Emts       = request.Emts       ?? new List<string>(),
            Status = DispatchStatus.Unassigned,
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
                EmsBaseCoords = mlResult.EmsBase?.BaseCoords == null ? null
                    : new GeoPoint { Lat = mlResult.EmsBase.BaseCoords.Lat, Lng = mlResult.EmsBase.BaseCoords.Lng },
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

        // Always set cancellation reason when cancelling
        if (parsed == DispatchStatus.Cancelled)
        {
            var reason = string.IsNullOrWhiteSpace(cancellationReason) ? null : cancellationReason.Trim();
            update = update.Set(d => d.CancellationReason, reason);
        }

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

            await _drivers.UpdateOneAsync(
                dr => dr.UserId == dispatch.AssignedDriverId, driverUpdate);
        }

        var detail = ToDetail(dispatch);
        await _hub.Clients.All.SendAsync("DispatchStatusUpdated", detail);
        return detail;
    }

    public async Task<DispatchDetailDto?> AssignDriverAsync(string id, string driverId)
    {
        var driver = await _drivers.Find(dr => dr.Id == driverId).FirstOrDefaultAsync();
        if (driver == null) return null;

        var userIdToStore = driver.UserId;

        // Add driver userId to both the primary field (first driver) and the list (all drivers).
        // AddToSet prevents duplicate entries in assignedDriverIds.
        var existingDispatch = await _dispatches.Find(d => d.Id == id).FirstOrDefaultAsync();
        if (existingDispatch == null) return null;

        var isFirstDriver = string.IsNullOrEmpty(existingDispatch.AssignedDriverId)
                            || existingDispatch.AssignedDriverIds.Count == 0;

        var update = Builders<Dispatch>.Update
            .Set(d => d.Status, DispatchStatus.Assigned)
            .Set(d => d.UpdatedAt, DateTime.UtcNow)
            .AddToSet(d => d.AssignedDriverIds, userIdToStore);

        // Only set the primary assignedDriverId for the first driver assigned
        if (isFirstDriver)
            update = update.Set(d => d.AssignedDriverId, userIdToStore);

        await _dispatches.UpdateOneAsync(d => d.Id == id, update);

        var driverUpdate = Builders<Driver>.Update
            .Set(dr => dr.Status, DriverStatus.Busy)
            .Set(dr => dr.ActiveDispatchId, id)
            .Set(dr => dr.UpdatedAt, DateTime.UtcNow);
        await _drivers.UpdateOneAsync(dr => dr.Id == driverId, driverUpdate);

        var dispatch = await _dispatches.Find(d => d.Id == id).FirstOrDefaultAsync();
        if (dispatch == null) return null;

        var detail = ToDetail(dispatch);

        // Notify ALL assigned drivers
        foreach (var uid in dispatch.AssignedDriverIds)
            await _hub.Clients.Group($"Driver_{uid}").SendAsync("DriverAssigned", detail);

        await _hub.Clients.Groups("Dispatcher", "EmsOperator")
            .SendAsync("DispatchStatusUpdated", detail);

        return detail;
    }

    private static DispatchSummaryDto ToSummary(Dispatch d) => new()
    {
        Id                 = d.Id!,
        PatientName        = d.PatientName,
        PatientAge         = d.PatientAge,
        Severity           = d.Severity,
        Condition          = d.Condition,
        Status             = d.Status.ToString(),
        AssignedDriverId   = d.AssignedDriverId,
        AssignedDriverIds  = d.AssignedDriverIds,
        HospitalName       = d.MlPrediction?.HospitalName,
        TotalTimeMin       = d.MlPrediction?.TimeComponents?.TotalTime,
        CreatedAt          = d.CreatedAt,
        CancellationReason = d.CancellationReason,
        DriverCompletions  = d.DriverCompletions,
        Paramedics         = d.Paramedics,
        Emts               = d.Emts,
    };

    private static DispatchDetailDto ToDetail(Dispatch d) => new()
    {
        Id                = d.Id!,
        PatientName       = d.PatientName,
        PatientAge        = d.PatientAge,
        Location          = new LocationDto { Lat = d.Location.Lat, Lng = d.Location.Lng, Address = d.Location.Address },
        Severity          = d.Severity,
        Condition         = d.Condition,
        Status            = d.Status.ToString(),
        AssignedDriverId  = d.AssignedDriverId,
        AssignedDriverIds = d.AssignedDriverIds,
        MlPrediction     = d.MlPrediction == null ? null : new MlPredictionDto
        {
            HospitalId             = d.MlPrediction.HospitalId,
            HospitalName           = d.MlPrediction.HospitalName,
            HospitalLevel          = d.MlPrediction.HospitalLevel,
            HospitalCoords         = d.MlPrediction.HospitalCoords == null ? null
                                        : new GeoPointDto { Lat = d.MlPrediction.HospitalCoords.Lat, Lng = d.MlPrediction.HospitalCoords.Lng },
            DistanceKm             = d.MlPrediction.DistanceKm,
            TimeComponents         = d.MlPrediction.TimeComponents == null ? null : new TimeComponentsDto
            {
                DispatchTime   = d.MlPrediction.TimeComponents.DispatchTime,
                TimeToPatient  = d.MlPrediction.TimeComponents.TimeToPatient,
                OnSceneTime    = d.MlPrediction.TimeComponents.OnSceneTime,
                TimeToHospital = d.MlPrediction.TimeComponents.TimeToHospital,
                HandoverTime   = d.MlPrediction.TimeComponents.HandoverTime,
                TotalTime      = d.MlPrediction.TimeComponents.TotalTime,
            },
            EmsBaseId              = d.MlPrediction.EmsBaseId,
            EmsBaseName            = d.MlPrediction.EmsBaseName,
            IsFallbackCalculation  = d.MlPrediction.IsFallbackCalculation,
            EmsBaseCoords          = d.MlPrediction.EmsBaseCoords == null ? null
                                        : new GeoPointDto { Lat = d.MlPrediction.EmsBaseCoords.Lat, Lng = d.MlPrediction.EmsBaseCoords.Lng },
        },
        CreatedAt          = d.CreatedAt,
        UpdatedAt          = d.UpdatedAt,
        CompletedAt        = d.CompletedAt,
        CancellationReason = d.CancellationReason,
        NumberOfAmbulances = d.NumberOfAmbulances,
        DriverCompletions  = d.DriverCompletions,
        Paramedics         = d.Paramedics,
        Emts               = d.Emts,
    };

    // ── Per-driver completion ─────────────────────────────────────────────
    public async Task<(DispatchDetailDto? dto, bool allDone)> CompleteDriverAsync(
        string dispatchId, string driverUserId)
    {
        var dispatch = await _dispatches.Find(d => d.Id == dispatchId).FirstOrDefaultAsync();
        if (dispatch == null) return (null, false);

        // Verify this driver is actually assigned
        var isAssigned = dispatch.AssignedDriverId == driverUserId
                         || dispatch.AssignedDriverIds.Contains(driverUserId);
        if (!isAssigned) return (null, false);

        // Mark this driver as done (idempotent — no-op if already done)
        dispatch.DriverCompletions[driverUserId] = true;

        // Check if every assigned driver has now completed
        var allDriversDone = dispatch.AssignedDriverIds.Count > 0
            && dispatch.AssignedDriverIds.All(uid => dispatch.DriverCompletions.ContainsKey(uid)
                                                     && dispatch.DriverCompletions[uid]);

        // Also handle legacy dispatches that only have AssignedDriverId (no list)
        if (!allDriversDone
            && dispatch.AssignedDriverIds.Count == 0
            && !string.IsNullOrEmpty(dispatch.AssignedDriverId))
        {
            allDriversDone = dispatch.DriverCompletions.ContainsKey(dispatch.AssignedDriverId)
                             && dispatch.DriverCompletions[dispatch.AssignedDriverId];
        }

        // Build MongoDB update
        var updateDef = Builders<Dispatch>.Update
            .Set($"driverCompletions.{driverUserId}", true)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        if (allDriversDone)
        {
            updateDef = updateDef
                .Set(d => d.Status, DispatchStatus.Completed)
                .Set(d => d.CompletedAt, DateTime.UtcNow);
        }

        await _dispatches.UpdateOneAsync(d => d.Id == dispatchId, updateDef);

        // Release THIS driver back to Available regardless of overall status
        var driverUpdateDef = Builders<Driver>.Update
            .Set(dr => dr.Status, DriverStatus.Available)
            .Set(dr => dr.ActiveDispatchId, (string?)null)
            .Set(dr => dr.UpdatedAt, DateTime.UtcNow);
        await _drivers.UpdateOneAsync(dr => dr.UserId == driverUserId, driverUpdateDef);

        // If all done, also release any remaining drivers in case of data inconsistency
        if (allDriversDone)
        {
            foreach (var uid in dispatch.AssignedDriverIds.Where(uid => uid != driverUserId))
            {
                await _drivers.UpdateOneAsync(
                    dr => dr.UserId == uid,
                    driverUpdateDef);
            }
        }

        var updated = await _dispatches.Find(d => d.Id == dispatchId).FirstOrDefaultAsync();
        if (updated == null) return (null, allDriversDone);

        var detail = ToDetail(updated);

        // Broadcast to all parties
        await _hub.Clients.All.SendAsync("DispatchStatusUpdated", detail);

        return (detail, allDriversDone);
    }
}

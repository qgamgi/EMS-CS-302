using EMS.API.DTOs;
using EMS.API.Hubs;
using EMS.API.Models;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;

namespace EMS.API.Services;

public interface IDriverService
{
    Task<List<DriverDto>> GetAllAsync();
    Task<DriverDto?> UpdateLocationAsync(string driverId, double lat, double lng);
}

public class DriverService : IDriverService
{
    private readonly IMongoCollection<Driver> _drivers;
    private readonly IMongoCollection<User> _users;
    private readonly IHubContext<DispatchHub> _hub;

    public DriverService(IMongoDatabase db, IHubContext<DispatchHub> hub)
    {
        _drivers = db.GetCollection<Driver>("drivers");
        _users = db.GetCollection<User>("users");
        _hub = hub;
    }

    public async Task<List<DriverDto>> GetAllAsync()
    {
        var drivers = await _drivers.Find(_ => true).ToListAsync();
        var userIds = drivers.Select(d => d.UserId).ToList();
        var users = await _users.Find(u => userIds.Contains(u.Id!)).ToListAsync();
        var userMap = users.ToDictionary(u => u.Id!);

        return drivers.Select(d =>
        {
            userMap.TryGetValue(d.UserId, out var user);
            return new DriverDto(
                d.Id!,
                d.UserId,
                user?.FullName ?? "Unknown",
                d.VehicleId,
                d.CurrentLocation?.Lat,
                d.CurrentLocation?.Lng,
                d.Status.ToString(),
                d.ActiveDispatchId
            );
        }).ToList();
    }

    public async Task<DriverDto?> UpdateLocationAsync(string userId, double lat, double lng)
    {
        var update = Builders<Driver>.Update
            .Set(d => d.CurrentLocation, new GeoPoint { Lat = lat, Lng = lng })
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        await _drivers.UpdateOneAsync(d => d.UserId == userId, update);

        var driver = await _drivers.Find(d => d.UserId == userId).FirstOrDefaultAsync();
        if (driver == null) return null;

        var user = await _users.Find(u => u.Id == driver.UserId).FirstOrDefaultAsync();

        var dto = new DriverDto(
            driver.Id!, driver.UserId, user?.FullName ?? "Unknown",
            driver.VehicleId, lat, lng,
            driver.Status.ToString(), driver.ActiveDispatchId
        );

        await _hub.Clients.Groups("Dispatcher", "EmsOperator")
            .SendAsync("DriverLocationUpdated", dto);

        return dto;
    }
}

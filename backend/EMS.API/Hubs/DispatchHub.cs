using System.Security.Claims;
using EMS.API.Models;
using EMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EMS.API.Hubs;

/// <summary>
/// SignalR hub for real-time EMS dispatch events.
///
/// Role-based groups:
///   - "Admin", "Dispatcher", "EmsOperator" — board-level groups
///   - "Driver_{id}"                          — per-driver group for personal assignment events
///
/// Client → Server methods:
///   - JoinGroup(role)             — join role group on connect
///   - UpdateDriverLocation(lat, lng) — GPS ping from Driver clients
/// </summary>
[Authorize]
public class DispatchHub : Hub
{
    private readonly IDriverService _driverService;

    public DispatchHub(IDriverService driverService)
    {
        _driverService = driverService;
    }

    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(role))
            await Groups.AddToGroupAsync(Context.ConnectionId, role);

        // Drivers also join a personal group so they can receive targeted assignment events
        if (role == UserRole.Driver.ToString() && !string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Driver_{userId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (!string.IsNullOrEmpty(role))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, role);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by Driver clients to push their GPS position.
    /// Persists to MongoDB and broadcasts to "Dispatcher" group.
    /// </summary>
    public async Task UpdateDriverLocation(double lat, double lng)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return;

        // driverId here is the User._id — DriverService maps userId → Driver
        await _driverService.UpdateLocationAsync(userId, lat, lng);
    }
}

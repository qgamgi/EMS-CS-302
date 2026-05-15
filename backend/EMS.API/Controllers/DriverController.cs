using System.Security.Claims;
using EMS.API.DTOs;
using EMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/drivers")]
[Authorize]
public class DriverController : ControllerBase
{
    private readonly IDriverService _driverService;

    public DriverController(IDriverService driverService)
    {
        _driverService = driverService;
    }

    /// <summary>List all drivers with current status (Dispatcher, Admin).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> GetAll()
    {
        var drivers = await _driverService.GetAllAsync();
        return Ok(drivers);
    }

    /// <summary>
    /// Update the calling driver's GPS location.
    /// The driver must be authenticated; their userId is taken from the JWT claim.
    /// </summary>
    [HttpPatch("location")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _driverService.UpdateLocationAsync(userId, request.Lat, request.Lng);
        if (result == null) return NotFound(new { message = "Driver profile not found for this user." });
        return Ok(result);
    }

    /// <summary>
    /// Set / clear a driver's availability for the day (Dispatcher or Admin).
    /// Pass unavailableUntil = null to restore the driver to Available.
    /// </summary>
    [HttpPatch("{id}/availability")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> SetAvailability(string id, [FromBody] SetDriverAvailabilityRequest request)
    {
        var result = await _driverService.SetAvailabilityAsync(id, request.UnavailableUntil, request.UnavailabilityReason);
        if (result == null) return NotFound(new { message = "Driver not found." });
        return Ok(result);
    }

    /// <summary>Set driver role type: Driver | Paramedic | EMT (Dispatcher or Admin).</summary>
    [HttpPatch("{id}/role-type")]
    [Authorize(Roles = "Admin,Dispatcher")]
    public async Task<IActionResult> SetRoleType(string id, [FromBody] SetDriverRoleTypeRequest request)
    {
        var result = await _driverService.SetRoleTypeAsync(id, request.RoleType);
        if (result == null) return NotFound(new { message = "Driver not found." });
        return Ok(result);
    }
}

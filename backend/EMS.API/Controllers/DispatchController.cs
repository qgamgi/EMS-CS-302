using System.Security.Claims;
using EMS.API.DTOs;
using EMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/dispatches")]
[Authorize]
public class DispatchController : ControllerBase
{
    private readonly IDispatchService _dispatchService;

    public DispatchController(IDispatchService dispatchService)
    {
        _dispatchService = dispatchService;
    }

    /// <summary>List all dispatches (Dispatcher, EmsOperator, Admin).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Dispatcher,EmsOperator")]
    public async Task<IActionResult> GetAll()
    {
        var dispatches = await _dispatchService.GetAllAsync();
        return Ok(dispatches);
    }

    /// <summary>
    /// Get active dispatch assigned to the calling driver.
    /// Filters by assignedDriverId == JWT userId, status not Completed/Cancelled.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> GetMyDispatch()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var dispatch = await _dispatchService.GetActiveForDriverAsync(userId);
        if (dispatch == null) return Ok(null);
        return Ok(dispatch);
    }

    /// <summary>Get dispatch by ID (all authenticated roles).</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var dispatch = await _dispatchService.GetByIdAsync(id);
        if (dispatch == null) return NotFound();
        return Ok(dispatch);
    }

    /// <summary>
    /// Create a new dispatch.
    /// Calls the ML service internally for hospital prediction.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Dispatcher")]
    public async Task<IActionResult> Create([FromBody] CreateDispatchRequest request)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var dispatch = await _dispatchService.CreateAsync(request, callerId);
        return CreatedAtAction(nameof(GetById), new { id = dispatch.Id }, dispatch);
    }

    /// <summary>
    /// Update dispatch status — for non-Completed transitions (EnRoute, OnScene,
    /// Transporting, Cancelled).  Drivers must use POST /{id}/complete to record
    /// their individual completion; pushing "Completed" via this endpoint is
    /// blocked for the Driver role to prevent one driver completing for everyone.
    /// </summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Driver,EmsOperator,Dispatcher,Admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateDispatchStatusRequest request)
    {
        // Drivers cannot force the overall status to Completed — they must use /complete
        var callerRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (callerRole == "Driver" &&
            string.Equals(request.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Drivers must use the /complete endpoint to mark their part done."
            });
        }

        try
        {
            var dispatch = await _dispatchService.UpdateStatusAsync(
                id, request.Status, request.CancellationReason);
            if (dispatch == null) return NotFound();
            return Ok(dispatch);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Called by a driver to mark their own part of a multi-driver dispatch as
    /// complete.  The dispatch only transitions to Completed once every assigned
    /// driver has called this endpoint.
    /// </summary>
    [HttpPost("{id}/complete")]
    [Authorize(Roles = "Driver")]
    public async Task<IActionResult> CompleteDriver(string id)
    {
        var driverUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(driverUserId))
            return Unauthorized(new { message = "Invalid token." });

        var (dto, allDone) = await _dispatchService.CompleteDriverAsync(id, driverUserId);
        if (dto == null)
            return NotFound(new { message = "Dispatch not found or you are not assigned to it." });

        return Ok(new { dispatch = dto, allCompleted = allDone });
    }

    /// <summary>Assign a driver to a dispatch (Dispatcher).</summary>
    [HttpPatch("{id}/assign")]
    [Authorize(Roles = "Dispatcher,Admin")]
    public async Task<IActionResult> AssignDriver(string id, [FromBody] AssignDriverRequest request)
    {
        var dispatch = await _dispatchService.AssignDriverAsync(id, request.DriverId);
        if (dispatch == null) return NotFound();
        return Ok(dispatch);
    }
}

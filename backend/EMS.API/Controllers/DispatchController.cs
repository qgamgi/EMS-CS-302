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

    /// <summary>Update dispatch status (Driver, EmsOperator).</summary>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Driver,EmsOperator,Dispatcher,Admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateDispatchStatusRequest request)
    {
        try
        {
            var dispatch = await _dispatchService.UpdateStatusAsync(
                id, request.status, request.cancellationReason);
            if (dispatch == null) return NotFound();
            return Ok(dispatch);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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

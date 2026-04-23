using EMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/sessions")]
[Authorize(Roles = "Admin")]
public class SessionController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>List all currently active sessions (Admin only).</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var sessions = await _sessionService.GetActiveAsync();
        var result = sessions.Select(s => new
        {
            s.Id,
            s.UserId,
            s.FullName,
            s.Email,
            s.Role,
            s.LoginAt,
            s.LogoutAt,
            s.IsActive,
            s.ExpiresAt,
        });
        return Ok(result);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using BCrypt.Net;
using EMS.API.DTOs;
using EMS.API.Models;
using EMS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ISessionService _sessionService;
    private readonly IMongoCollection<User> _users;

    public AuthController(
        IAuthService authService,
        ISessionService sessionService,
        IMongoDatabase db)
    {
        _authService    = authService;
        _sessionService = sessionService;
        _users          = db.GetCollection<User>("users");
    }

    /// <summary>Register a new user (Admin only).</summary>
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var user = await _authService.RegisterAsync(request);
            return CreatedAtAction(nameof(Register),
                new { id = user.Id },
                new { user.Id, user.FullName, user.Email, Role = user.Role.ToString() });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Login and receive a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(result);
    }

    /// <summary>
    /// Change the authenticated user's own password.
    /// Requires the current password, rejects if the new password matches
    /// the current one, and enforces the same strength rules as registration.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { message = "Invalid token." });

        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
            return NotFound(new { message = "User not found." });

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        // Reject if new password is the same as the current one
        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
            return BadRequest(new { message = "New password must be different from the current password." });

        // Strength rules: min 8 chars, 1 uppercase, 1 special character
        if (request.NewPassword.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });
        if (!request.NewPassword.Any(char.IsUpper))
            return BadRequest(new { message = "Password must contain at least one uppercase letter." });
        if (!request.NewPassword.Any(c => !char.IsLetterOrDigit(c)))
            return BadRequest(new { message = "Password must contain at least one special character." });

        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12));
        await _users.UpdateOneAsync(u => u.Id == userId, update);

        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>Logout — marks the session as inactive in MongoDB.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (!string.IsNullOrEmpty(jti))
            await _sessionService.EndAsync(jti);

        return Ok(new { message = "Logged out successfully." });
    }
}

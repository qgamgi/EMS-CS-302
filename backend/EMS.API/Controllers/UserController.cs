using System.Text.Json.Serialization;
using BCrypt.Net;
using EMS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace EMS.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UserController : ControllerBase
{
    private readonly IMongoCollection<User> _users;

    public UserController(IMongoDatabase db)
    {
        _users = db.GetCollection<User>("users");
    }

    /// <summary>List all users (Admin only).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _users.Find(_ => true).ToListAsync();
        var result = users.Select(u => new
        {
            u.Id, u.FullName, u.Email,
            Role = u.Role.ToString(),
            u.IsActive, u.CreatedAt, u.LastSeenAt
        });
        return Ok(result);
    }

    /// <summary>Deactivate a user account (Admin only).</summary>
    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(string id)
    {
        var update = Builders<User>.Update.Set(u => u.IsActive, false);
        var result = await _users.UpdateOneAsync(u => u.Id == id, update);

        if (result.MatchedCount == 0) return NotFound();
        return Ok(new { message = "User deactivated." });
    }

    /// <summary>Reactivate a user account (Admin only).</summary>
    [HttpPatch("{id}/activate")]
    public async Task<IActionResult> Activate(string id)
    {
        var update = Builders<User>.Update.Set(u => u.IsActive, true);
        var result = await _users.UpdateOneAsync(u => u.Id == id, update);

        if (result.MatchedCount == 0) return NotFound();
        return Ok(new { message = "User activated." });
    }

    /// <summary>Edit user details: fullName, email, role, and optionally password (Admin only).</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { message = "User not found." });

        // Validate role if provided
        if (!string.IsNullOrEmpty(request.Role) && !Enum.TryParse<UserRole>(request.Role, out _))
            return BadRequest(new { message = $"Invalid role: {request.Role}" });

        // Check email uniqueness if email is changing
        if (!string.IsNullOrEmpty(request.Email) &&
            !request.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _users.Find(u => u.Email == request.Email && u.Id != id)
                                     .AnyAsync();
            if (exists)
                return Conflict(new { message = "An account with that email already exists." });
        }

        // Validate new password strength if provided
        if (!string.IsNullOrEmpty(request.Password))
        {
            if (request.Password.Length < 8)
                return BadRequest(new { message = "Password must be at least 8 characters." });
            if (!request.Password.Any(char.IsUpper))
                return BadRequest(new { message = "Password must contain at least one uppercase letter." });
            if (!request.Password.Any(c => !char.IsLetterOrDigit(c)))
                return BadRequest(new { message = "Password must contain at least one special character." });
        }

        var updates = new List<UpdateDefinition<User>>();
        if (!string.IsNullOrEmpty(request.FullName))
            updates.Add(Builders<User>.Update.Set(u => u.FullName, request.FullName.Trim()));
        if (!string.IsNullOrEmpty(request.Email))
            updates.Add(Builders<User>.Update.Set(u => u.Email, request.Email.Trim().ToLower()));
        if (!string.IsNullOrEmpty(request.Role) && Enum.TryParse<UserRole>(request.Role, out var newRole))
            updates.Add(Builders<User>.Update.Set(u => u.Role, newRole));
        if (!string.IsNullOrEmpty(request.Password))
            updates.Add(Builders<User>.Update.Set(u => u.PasswordHash,
                BCrypt.Net.BCrypt.HashPassword(request.Password)));

        if (updates.Count == 0)
            return BadRequest(new { message = "No fields to update." });

        var combined = Builders<User>.Update.Combine(updates);
        await _users.UpdateOneAsync(u => u.Id == id, combined);

        var updated = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
        return Ok(new
        {
            updated!.Id, updated.FullName, updated.Email,
            Role = updated.Role.ToString(),
            updated.IsActive, updated.CreatedAt
        });
    }
}

public class UpdateUserRequest
{
    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

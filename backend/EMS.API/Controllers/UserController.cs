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
}

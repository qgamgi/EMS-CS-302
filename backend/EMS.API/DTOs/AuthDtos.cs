using System.Text.Json.Serialization;

namespace EMS.API.DTOs;

public class ChangePasswordRequest
{
    [JsonPropertyName("currentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [JsonPropertyName("newPassword")]
    public string NewPassword { get; set; } = string.Empty;
}

public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string Role
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    string Token,
    string UserId,
    string FullName,
    string Email,
    string Role
);

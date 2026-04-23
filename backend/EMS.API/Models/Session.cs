using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMS.API.Models;

public class Session
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>The JWT jti (unique token ID) — used to identify the session.</summary>
    [BsonElement("jti")]
    public string Jti { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("fullName")]
    public string FullName { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;

    [BsonElement("loginAt")]
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null while session is active; set on logout.</summary>
    [BsonElement("logoutAt")]
    public DateTime? LogoutAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// MongoDB TTL field — automatically expires the document 24 h after login
    /// if the client never calls logout. The TTL index is created in 05_create_sessions_index.js.
    /// </summary>
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);
}

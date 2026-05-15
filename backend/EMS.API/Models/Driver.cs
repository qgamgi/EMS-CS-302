using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMS.API.Models;

public enum DriverStatus
{
    Available,
    Busy,
    Offline
}

public class Driver
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("vehicleId")]
    public string VehicleId { get; set; } = string.Empty;

    [BsonElement("currentLocation")]
    public GeoPoint? CurrentLocation { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public DriverStatus Status { get; set; } = DriverStatus.Offline;

    [BsonElement("activeDispatchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ActiveDispatchId { get; set; }

    /// <summary>
    /// When set to a future date, the driver is marked unavailable for that day.
    /// Null = no manual override (status is derived from DriverStatus).
    /// </summary>
    [BsonElement("unavailableUntil")]
    public DateTime? UnavailableUntil { get; set; }

    /// <summary>Free-text reason for unavailability (e.g. "Day Off", "On Leave").</summary>
    [BsonElement("unavailabilityReason")]
    public string? UnavailabilityReason { get; set; }

    /// <summary>Role of the driver: Driver, Paramedic, EMT.</summary>
    [BsonElement("roleType")]
    public string RoleType { get; set; } = "Driver";

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

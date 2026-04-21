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

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

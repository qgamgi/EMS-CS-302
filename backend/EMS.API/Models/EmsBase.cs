using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMS.API.Models;

public class EmsBase
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("baseId")]
    public int BaseId { get; set; }

    [BsonElement("baseName")]
    public string BaseName { get; set; } = string.Empty;

    [BsonElement("barangay")]
    public string Barangay { get; set; } = string.Empty;

    [BsonElement("location")]
    public GeoJsonPoint? Location { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
}

public class GeoJsonPoint
{
    [BsonElement("type")]
    public string Type { get; set; } = "Point";

    /// <summary>GeoJSON: [longitude, latitude]</summary>
    [BsonElement("coordinates")]
    public double[] Coordinates { get; set; } = Array.Empty<double>();
}

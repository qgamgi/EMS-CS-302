using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EMS.API.Models;

public enum DispatchStatus
{
    Pending,
    Assigned,
    EnRoute,
    OnScene,
    Transporting,
    Completed,
    Cancelled
}

public class GeoPoint
{
    [BsonElement("lat")]
    public double Lat { get; set; }

    [BsonElement("lng")]
    public double Lng { get; set; }
}

public class PatientLocation
{
    [BsonElement("lat")]
    public double Lat { get; set; }

    [BsonElement("lng")]
    public double Lng { get; set; }

    [BsonElement("address")]
    public string Address { get; set; } = string.Empty;
}

public class TimeComponents
{
    [BsonElement("dispatch_time")]
    public double DispatchTime { get; set; }

    [BsonElement("time_to_patient")]
    public double TimeToPatient { get; set; }

    [BsonElement("on_scene_time")]
    public double OnSceneTime { get; set; }

    [BsonElement("time_to_hospital")]
    public double TimeToHospital { get; set; }

    [BsonElement("handover_time")]
    public double HandoverTime { get; set; }

    [BsonElement("total_time")]
    public double TotalTime { get; set; }
}

public class MlPrediction
{
    [BsonElement("hospital_id")]
    public int HospitalId { get; set; }

    [BsonElement("hospital_name")]
    public string HospitalName { get; set; } = string.Empty;

    [BsonElement("hospital_level")]
    public int? HospitalLevel { get; set; }

    [BsonElement("hospital_coords")]
    public GeoPoint? HospitalCoords { get; set; }

    [BsonElement("distance_km")]
    public double DistanceKm { get; set; }

    [BsonElement("time_components")]
    public TimeComponents? TimeComponents { get; set; }

    [BsonElement("ems_base_id")]
    public int EmsBaseId { get; set; }

    [BsonElement("ems_base_name")]
    public string EmsBaseName { get; set; } = string.Empty;

    [BsonElement("is_fallback_calculation")]
    public bool IsFallbackCalculation { get; set; }
}

public class Dispatch
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("callerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string CallerId { get; set; } = string.Empty;

    [BsonElement("patientName")]
    public string PatientName { get; set; } = string.Empty;

    [BsonElement("location")]
    public PatientLocation Location { get; set; } = new();

    [BsonElement("severity")]
    public string Severity { get; set; } = string.Empty;

    [BsonElement("condition")]
    public string Condition { get; set; } = string.Empty;

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public DispatchStatus Status { get; set; } = DispatchStatus.Pending;

    [BsonElement("assignedDriverId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? AssignedDriverId { get; set; }

    [BsonElement("assignedEmsBaseId")]
    public int? AssignedEmsBaseId { get; set; }

    [BsonElement("mlPrediction")]
    public MlPrediction? MlPrediction { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }
}

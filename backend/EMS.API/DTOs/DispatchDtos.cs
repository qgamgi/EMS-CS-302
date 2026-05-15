using System.Text.Json.Serialization;
using EMS.API.Models;

namespace EMS.API.DTOs;

public record CreateDispatchRequest(
    string PatientName,
    int? PatientAge,
    double Latitude,
    double Longitude,
    string Address,
    string Severity,
    string Condition,
    int NumberOfAmbulances = 1,
    List<string>? Paramedics = null,
    List<string>? Emts = null
);

// Plain class with explicit [JsonPropertyName] so the deserializer always
// matches the camelCase keys Flutter sends, regardless of global naming policy.
public class UpdateDispatchStatusRequest
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; set; }
}

public class AssignDriverRequest
{
    [JsonPropertyName("driverId")]
    public string DriverId { get; set; } = string.Empty;
}

/// <summary>
/// Sent by a driver when they mark their own part of a dispatch as complete.
/// The driverId (userId) comes from the JWT, not the body.
/// </summary>
public class CompleteDriverRequest
{
    // intentionally empty — driverId is extracted from the JWT claim
}

public class DispatchSummaryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("patientName")]
    public string PatientName { get; set; } = string.Empty;

    [JsonPropertyName("patientAge")]
    public int? PatientAge { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("assignedDriverId")]
    public string? AssignedDriverId { get; set; }

    [JsonPropertyName("assignedDriverIds")]
    public List<string> AssignedDriverIds { get; set; } = new();

    [JsonPropertyName("hospitalName")]
    public string? HospitalName { get; set; }

    [JsonPropertyName("totalTimeMin")]
    public double? TotalTimeMin { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; set; }

    [JsonPropertyName("driverCompletions")]
    public Dictionary<string, bool> DriverCompletions { get; set; } = new();

    [JsonPropertyName("paramedics")]
    public List<string> Paramedics { get; set; } = new();

    [JsonPropertyName("emts")]
    public List<string> Emts { get; set; } = new();
}

public class DispatchDetailDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("patientName")]
    public string PatientName { get; set; } = string.Empty;

    [JsonPropertyName("patientAge")]
    public int? PatientAge { get; set; }

    [JsonPropertyName("location")]
    public LocationDto Location { get; set; } = new();

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("assignedDriverId")]
    public string? AssignedDriverId { get; set; }

    [JsonPropertyName("assignedDriverIds")]
    public List<string> AssignedDriverIds { get; set; } = new();

    [JsonPropertyName("mlPrediction")]
    public MlPredictionDto? MlPrediction { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; set; }

    [JsonPropertyName("numberOfAmbulances")]
    public int NumberOfAmbulances { get; set; } = 1;

    [JsonPropertyName("driverCompletions")]
    public Dictionary<string, bool> DriverCompletions { get; set; } = new();

    [JsonPropertyName("paramedics")]
    public List<string> Paramedics { get; set; } = new();

    [JsonPropertyName("emts")]
    public List<string> Emts { get; set; } = new();
}

public class LocationDto
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;
}

public class MlPredictionDto
{
    [JsonPropertyName("hospitalId")]
    public int HospitalId { get; set; }

    [JsonPropertyName("hospitalName")]
    public string HospitalName { get; set; } = string.Empty;

    [JsonPropertyName("hospitalLevel")]
    public int? HospitalLevel { get; set; }

    [JsonPropertyName("hospitalCoords")]
    public GeoPointDto? HospitalCoords { get; set; }

    [JsonPropertyName("distanceKm")]
    public double DistanceKm { get; set; }

    [JsonPropertyName("timeComponents")]
    public TimeComponentsDto? TimeComponents { get; set; }

    [JsonPropertyName("emsBaseId")]
    public int EmsBaseId { get; set; }

    [JsonPropertyName("emsBaseName")]
    public string EmsBaseName { get; set; } = string.Empty;

    [JsonPropertyName("isFallbackCalculation")]
    public bool IsFallbackCalculation { get; set; }

    [JsonPropertyName("emsBaseCoords")]
    public GeoPointDto? EmsBaseCoords { get; set; }
}

public class GeoPointDto
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

public class TimeComponentsDto
{
    [JsonPropertyName("dispatchTime")]
    public double DispatchTime { get; set; }

    [JsonPropertyName("timeToPatient")]
    public double TimeToPatient { get; set; }

    [JsonPropertyName("onSceneTime")]
    public double OnSceneTime { get; set; }

    [JsonPropertyName("timeToHospital")]
    public double TimeToHospital { get; set; }

    [JsonPropertyName("handoverTime")]
    public double HandoverTime { get; set; }

    [JsonPropertyName("totalTime")]
    public double TotalTime { get; set; }
}

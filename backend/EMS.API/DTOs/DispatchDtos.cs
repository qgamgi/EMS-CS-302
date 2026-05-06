using EMS.API.Models;

namespace EMS.API.DTOs;

public record CreateDispatchRequest(
    string PatientName,
    double Latitude,
    double Longitude,
    string Address,
    string Severity,
    string Condition,
    int NumberOfAmbulances = 1
);

// Using a record with explicit JsonPropertyName attributes for reliable binding.
// Flutter sends: {"status": "Cancelled", "cancellationReason": "reason"}
public class UpdateDispatchStatusRequest
{
    public string status { get; set; } = string.Empty;
    public string? cancellationReason { get; set; }
}

public record AssignDriverRequest(
    string DriverId
);

public record DispatchSummaryDto(
    string Id,
    string PatientName,
    string Severity,
    string Condition,
    string Status,
    string? AssignedDriverId,
    string? HospitalName,
    double? TotalTimeMin,
    DateTime CreatedAt
);

public record DispatchDetailDto(
    string Id,
    string PatientName,
    LocationDto Location,
    string Severity,
    string Condition,
    string Status,
    string? AssignedDriverId,
    MlPredictionDto? MlPrediction,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt,
    string? CancellationReason = null,
    int NumberOfAmbulances = 1
);

public record LocationDto(double Lat, double Lng, string Address);

public record MlPredictionDto(
    int HospitalId,
    string HospitalName,
    int? HospitalLevel,
    GeoPointDto? HospitalCoords,
    double DistanceKm,
    TimeComponentsDto? TimeComponents,
    int EmsBaseId,
    string EmsBaseName,
    bool IsFallbackCalculation,
    GeoPointDto? EmsBaseCoords = null
);

public record GeoPointDto(double Lat, double Lng);

public record TimeComponentsDto(
    double DispatchTime,
    double TimeToPatient,
    double OnSceneTime,
    double TimeToHospital,
    double HandoverTime,
    double TotalTime
);

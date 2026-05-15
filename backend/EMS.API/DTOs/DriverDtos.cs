using System.Text.Json.Serialization;

namespace EMS.API.DTOs;

public record UpdateLocationRequest(double Lat, double Lng);

public record DriverDto(
    string Id,
    string UserId,
    string FullName,
    string VehicleId,
    double? Lat,
    double? Lng,
    string Status,
    string? ActiveDispatchId,
    DateTime? UnavailableUntil = null,
    string? UnavailabilityReason = null,
    string RoleType = "Driver"
);

public class SetDriverAvailabilityRequest
{
    /// <summary>
    /// Set to a future DateTime (UTC) to mark the driver unavailable until that time.
    /// Set to null to clear unavailability and restore to Available.
    /// </summary>
    [JsonPropertyName("unavailableUntil")]
    public DateTime? UnavailableUntil { get; set; }

    [JsonPropertyName("unavailabilityReason")]
    public string? UnavailabilityReason { get; set; }
}

public class SetDriverRoleTypeRequest
{
    [JsonPropertyName("roleType")]
    public string RoleType { get; set; } = "Driver";
}

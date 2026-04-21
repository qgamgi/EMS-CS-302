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
    string? ActiveDispatchId
);

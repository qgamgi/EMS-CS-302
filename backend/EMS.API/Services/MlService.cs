using System.Text.Json;
using System.Text.Json.Serialization;

namespace EMS.API.Services;

/// <summary>
/// HTTP client wrapper that calls the FastAPI ML service.
/// </summary>
public interface IMlService
{
    Task<MlPredictionResult> PredictAsync(double lat, double lng, string severity, string condition);
}

public class MlService : IMlService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public MlService(HttpClient http)
    {
        _http = http;
    }

    public async Task<MlPredictionResult> PredictAsync(
        double lat, double lng, string severity, string condition)
    {
        var payload = new
        {
            latitude = lat,
            longitude = lng,
            severity,
            condition,
        };

        var response = await _http.PostAsJsonAsync("/predict", payload, _jsonOpts);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MlPredictionResult>(_jsonOpts);
        return result ?? throw new InvalidOperationException("Empty ML response.");
    }
}

// ─── Deserialization POCOs (mirror FastAPI response) ─────────────────────────

public class MlPredictionResult
{
    [JsonPropertyName("hospital_id")]
    public int HospitalId { get; set; }

    [JsonPropertyName("hospital_name")]
    public string HospitalName { get; set; } = string.Empty;

    [JsonPropertyName("hospital_level")]
    public int? HospitalLevel { get; set; }

    [JsonPropertyName("hospital_coords")]
    public MlGeoPoint? HospitalCoords { get; set; }

    [JsonPropertyName("ems_base")]
    public MlEmsBase? EmsBase { get; set; }

    [JsonPropertyName("distance_km")]
    public double DistanceKm { get; set; }

    [JsonPropertyName("time_components")]
    public MlTimeComponents? TimeComponents { get; set; }

    [JsonPropertyName("is_fallback_calculation")]
    public bool IsFallbackCalculation { get; set; }
}

public class MlGeoPoint
{
    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }
}

public class MlEmsBase
{
    [JsonPropertyName("base_id")]
    public int BaseId { get; set; }

    [JsonPropertyName("base_name")]
    public string BaseName { get; set; } = string.Empty;

    // Coordinates returned by the ML service (if available)
    [JsonPropertyName("base_lat")]
    public double? BaseLat { get; set; }

    [JsonPropertyName("base_lng")]
    public double? BaseLng { get; set; }
}

/// <summary>
/// Fallback coordinates for known Marikina EMS bases, keyed by base_id.
/// Used when the ML service does not return base_lat/base_lng.
/// </summary>
public static class EmsBaseFallbackCoords
{
    private static readonly Dictionary<int, (double Lat, double Lng)> _coords = new()
    {
        { 1, (14.6507, 121.1029) }, // Marikina City Main EMS Base
        { 2, (14.6398, 121.1105) }, // Concepcion EMS Sub-station
        { 3, (14.6601, 121.0953) }, // Nangka EMS Sub-station
        { 4, (14.6473, 121.0872) }, // Tumana EMS Sub-station
        { 5, (14.6550, 121.1200) }, // Parang EMS Sub-station
    };

    public static (double Lat, double Lng)? Get(int baseId) =>
        _coords.TryGetValue(baseId, out var c) ? c : null;
}

public class MlTimeComponents
{
    [JsonPropertyName("dispatch_time")]
    public double DispatchTime { get; set; }

    [JsonPropertyName("time_to_patient")]
    public double TimeToPatient { get; set; }

    [JsonPropertyName("on_scene_time")]
    public double OnSceneTime { get; set; }

    [JsonPropertyName("time_to_hospital")]
    public double TimeToHospital { get; set; }

    [JsonPropertyName("handover_time")]
    public double HandoverTime { get; set; }

    [JsonPropertyName("total_time")]
    public double TotalTime { get; set; }
}

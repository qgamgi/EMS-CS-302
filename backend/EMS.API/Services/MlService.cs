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

    [JsonPropertyName("base_coords")]
    public MlGeoPoint? BaseCoords { get; set; }
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

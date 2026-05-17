using System.Net.Http.Json;
using System.Text.Json;

namespace Nahel.Cli;

public sealed class NahelApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public NahelApiClient(string baseUrl = "http://127.0.0.1:11435")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<JsonDocument?> GetAsync(string path)
    {
        try
        {
            var response = await _http.GetAsync($"{_baseUrl}{path}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return JsonDocument.Parse("{}");
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<JsonDocument?> PostAsync(string path, object payload)
    {
        try
        {
            var content = JsonContent.Create(payload, options: _jsonOptions);
            var response = await _http.PostAsync($"{_baseUrl}{path}", content);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return JsonDocument.Parse("{}");
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<JsonDocument?> DeleteAsync(string path)
    {
        try
        {
            var response = await _http.DeleteAsync($"{_baseUrl}{path}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json)) return JsonDocument.Parse("{}");
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}

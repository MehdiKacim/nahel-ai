using System.Text.Json;
using System.Net.Http.Json;
using Nahel.SDK.Models;

namespace Nahel.Engine.Ovms;

public sealed class OvmsHealthClient
{
    private readonly HttpClient _httpClient;

    public OvmsHealthClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EngineHealth> GetHealthAsync(string host, int restPort, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"http://{host}:{restPort}/v3/models", ct);
            return new EngineHealth("ovms", response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "healthy" : $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new EngineHealth("ovms", false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> ListLoadedModelsAsync(string host, int restPort, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"http://{host}:{restPort}/v3/models", ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<string>();
            using var jsonDoc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (jsonDoc is null) return Array.Empty<string>();
            var json = jsonDoc.RootElement;
            if (json.ValueKind == JsonValueKind.Array)
                return json.EnumerateArray().Select(m => m.GetProperty("name").GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            if (json.TryGetProperty("models", out var models))
                return models.EnumerateArray().Select(m => m.GetProperty("name").GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToList();
            return Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }
}

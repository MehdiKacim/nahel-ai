using System.Net.Http.Json;
using System.Text.Json;

namespace Ollamock.App.Services;

public class BridgeService
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;

    public BridgeService(string baseUrl)
    {
        _baseUrl = baseUrl;
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<BridgeStatus?> GetStatusAsync()
    {
        try { return await _client.GetFromJsonAsync<BridgeStatus>($"{_baseUrl}/admin/status"); }
        catch { return null; }
    }

    public async Task<List<ModelInfo>?> GetModelsAsync()
    {
        try { return await _client.GetFromJsonAsync<List<ModelInfo>>($"{_baseUrl}/admin/models"); }
        catch { return null; }
    }

    public async Task<List<BackendInfo>?> GetBackendsAsync()
    {
        try { return await _client.GetFromJsonAsync<List<BackendInfo>>($"{_baseUrl}/admin/backends"); }
        catch { return null; }
    }

    public async Task<List<LauncherInfo>?> GetLaunchersAsync()
    {
        try { return await _client.GetFromJsonAsync<List<LauncherInfo>>($"{_baseUrl}/admin/launchers"); }
        catch { return null; }
    }

    public async Task<bool> ToggleModelAsync(string model)
    {
        try { var response = await _client.PostAsync($"{_baseUrl}/admin/models/{model}/toggle", null); return response.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> StartBackendAsync(string backend)
    {
        try { var response = await _client.PostAsync($"{_baseUrl}/admin/backends/{backend}/start", null); return response.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> StopBackendAsync(string backend)
    {
        try { var response = await _client.PostAsync($"{_baseUrl}/admin/backends/{backend}/stop", null); return response.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> RestartBackendAsync(string backend)
    {
        try { var response = await _client.PostAsync($"{_baseUrl}/admin/backends/{backend}/restart", null); return response.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> LaunchToolAsync(string tool)
    {
        try { var response = await _client.PostAsync($"{_baseUrl}/admin/launchers/{tool}/launch", null); return response.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> InstallToolAsync(string tool)
    {
        try { var response = await _client.PostAsync($"{_baseUrl}/admin/launchers/{tool}/install", null); return response.IsSuccessStatusCode; }
        catch { return false; }
    }

    public async Task<bool> ConfigureToolAsync(string tool, string? model = null)
    {
        try
        {
            var body = new { bridgeUrl = _baseUrl, model };
            var response = await _client.PostAsJsonAsync($"{_baseUrl}/admin/launchers/{tool}/configure", body);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<string>?> GetLogsAsync(int count = 50)
    {
        try { return await _client.GetFromJsonAsync<List<string>>($"{_baseUrl}/admin/logs?count={count}"); }
        catch { return null; }
    }

    public async Task<List<string>?> GetMetricsAsync(int count = 30)
    {
        try { return await _client.GetFromJsonAsync<List<string>>($"{_baseUrl}/admin/metrics?count={count}"); }
        catch { return null; }
    }

    public async Task<string?> SendPromptAsync(string model, string prompt)
    {
        try
        {
            var response = await _client.PostAsJsonAsync($"{_baseUrl}/api/generate", new { model, prompt, stream = false });
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("response").GetString();
        }
        catch { return null; }
    }
}

public class BridgeStatus
{
    public string bridge { get; set; } = "";
    public string ollamaNative { get; set; } = "";
    public long ramUsed { get; set; }
    public List<BackendStatus> backends { get; set; } = new();
    public DateTime timestamp { get; set; }
}

public class BackendStatus
{
    public string id { get; set; } = "";
    public int port { get; set; }
    public string type { get; set; } = "";
    public bool running { get; set; }
    public int restartCount { get; set; }
    public string? runtimeVersion { get; set; }
}

public class ModelInfo
{
    public string name { get; set; } = "";
    public string provider { get; set; } = "";
    public string device { get; set; } = "";
    public string? fallbackDevice { get; set; }
    public string path { get; set; } = "";
    public int? contextSize { get; set; }
    public string status { get; set; } = "";
    public bool loaded { get; set; }
    public int? pid { get; set; }
    public string? uptime { get; set; }
    public long ramUsed { get; set; }
}

public class BackendInfo
{
    public string id { get; set; } = "";
    public int port { get; set; }
    public string type { get; set; } = "";
    public string executable { get; set; } = "";
    public bool running { get; set; }
    public int restartCount { get; set; }
    public string? runtimeVersion { get; set; }
}

public class LauncherInfo
{
    public string name { get; set; } = "";
    public string displayName { get; set; } = "";
    public string description { get; set; } = "";
    public string apiFormat { get; set; } = "";
    public string category { get; set; } = "";
    public bool installed { get; set; }
    public string? version { get; set; }
    public string? path { get; set; }
    public bool running { get; set; }
    public bool configured { get; set; }
    public bool canInstall { get; set; }
    public string? installCommand { get; set; }
    public string? installHint { get; set; }
    public string? homepageUrl { get; set; }
    public string? documentationUrl { get; set; }
}

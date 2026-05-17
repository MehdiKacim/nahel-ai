using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Engine.Ovms;

public sealed class OvmsEngine : IBackend, IBackendInstaller, IBackendUpdater, IOpenAiBackend
{
    private OvmsOptions _options;
    private readonly OvmsProcessSupervisor _supervisor;
    private readonly OvmsHealthClient _healthClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OvmsEngine> _logger;
    private readonly string _apiPrefix;

    public string EngineId => _options.EngineId;
    public string DisplayName => _options.DisplayName;
    public string EngineType => "ovms";

    public OvmsEngine(
        OvmsOptions options,
        OvmsProcessSupervisor supervisor,
        OvmsHealthClient healthClient,
        HttpClient httpClient,
        ILogger<OvmsEngine> logger)
    {
        _options = options;
        _supervisor = supervisor;
        _healthClient = healthClient;
        _httpClient = httpClient;
        _logger = logger;
        _apiPrefix = !string.IsNullOrEmpty(options.ModelPath) && File.Exists(Path.Combine(options.ModelPath, "graph.pbtxt"))
            ? "/v3"
            : "/v1";
    }

    public Task<EngineStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineStatus(EngineId, _supervisor.IsRunning ? "running" : "stopped", _supervisor.IsRunning, _supervisor.IsRunning ? DateTimeOffset.UtcNow : null));

    public async Task<EngineHealth> GetHealthAsync(CancellationToken ct = default)
        => await _healthClient.GetHealthAsync("127.0.0.1", _options.RestPort, ct);

    public Task<EngineCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineCapabilities
        {
            SupportsMultiModel = true,
            SupportsHotSwap = true,
            SupportsUnloadModel = true,
            SupportsOpenAiApi = true,
            SupportsStreaming = true,
            SupportsUpdate = true,
            SupportsRuntimeUpdate = true,
            SupportsMetrics = true
        });

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_supervisor.IsRunning) return;
        var started = await _supervisor.StartAsync(_options, ct);
        if (!started) throw new Exception("Failed to start OVMS.");
    }

    public Task StopAsync(CancellationToken ct = default) => _supervisor.StopAsync(ct);

    public async Task RestartAsync(CancellationToken ct = default)
    {
        await _supervisor.RestartAsync(_options, ct);
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        var models = new List<ModelInfo>();
        if (!string.IsNullOrEmpty(_options.ModelName))
        {
            models.Add(new ModelInfo(_options.ModelName, _options.ModelName, EngineId, _options.ModelName, _options.ModelPath));
        }
        return Task.FromResult<IReadOnlyList<ModelInfo>>(models);
    }

    public async Task<ModelSwitchResult> SwitchModelAsync(ModelSwitchRequest request, CancellationToken ct = default)
    {
        await _supervisor.StopAsync(ct);
        // Update options with new model info
        var newOptions = _options with
        {
            ModelName = request.ModelId,
            ModelPath = request.TargetEngineModelName ?? _options.ModelPath
        };
        var started = await _supervisor.StartAsync(newOptions, ct);
        if (started)
        {
            _options = newOptions;
            return new ModelSwitchResult(true, null);
        }
        return new ModelSwitchResult(false, "Failed to switch model.");
    }

    public Task<EngineInstallStatus> GetInstallStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineInstallStatus(EngineId, false, null));

    public Task<EngineInstallResult> InstallAsync(EngineInstallRequest request, CancellationToken ct = default)
        => Task.FromResult(new EngineInstallResult(false, "OVMS auto-install not implemented."));

    public Task<EngineVerifyResult> VerifyAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineVerifyResult(false, "Verification not implemented."));

    public Task<EngineVersionInfo> GetVersionAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineVersionInfo(EngineId, _options.OpenVinoVersion, _options.VersionPolicy));

    public Task<EngineUpdateResult> UpdateEngineAsync(EngineUpdateRequest request, CancellationToken ct = default)
        => Task.FromResult(new EngineUpdateResult(false, "Engine update not implemented."));

    public Task<EngineUpdateResult> UpdateRuntimeAsync(EngineUpdateRequest request, CancellationToken ct = default)
        => Task.FromResult(new EngineUpdateResult(false, "Runtime update not implemented."));

    // IOpenAiBackend — proxy to OVMS native OpenAI-compatible endpoints
    public async Task<OpenAiModelListResponse> ListOpenAiModelsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"http://127.0.0.1:{_options.RestPort}{_apiPrefix}/models", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OpenAiModelListResponse>(OpenAiJsonOptions.Default, ct)
            ?? throw new InvalidOperationException("Invalid response from OVMS.");
    }

    public async Task<OpenAiChatCompletionResponse> CreateChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"http://127.0.0.1:{_options.RestPort}{_apiPrefix}/chat/completions",
            request,
            OpenAiJsonOptions.Default,
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(OpenAiJsonOptions.Default, ct)
            ?? throw new InvalidOperationException("Invalid response from OVMS.");
    }

    public async IAsyncEnumerable<OpenAiChatCompletionChunk> StreamChatCompletionAsync(OpenAiChatCompletionRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_options.RestPort}{_apiPrefix}/chat/completions");
        req.Content = JsonContent.Create(request, options: OpenAiJsonOptions.Default);
        using var response = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") yield break;
            var chunk = JsonSerializer.Deserialize<OpenAiChatCompletionChunk>(data, OpenAiJsonOptions.Default);
            if (chunk != null) yield return chunk;
        }
    }
}

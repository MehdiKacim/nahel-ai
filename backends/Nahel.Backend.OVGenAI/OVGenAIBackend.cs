using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Engine.OVGenAI;

public sealed class OVGenAIBackend : IBackend, IOpenAiBackend
{
    private OVGenAIOptions _options;
    private readonly OVGenAIProcessSupervisor _supervisor;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OVGenAIBackend> _logger;

    public string EngineId => _options.EngineId;
    public string DisplayName => _options.DisplayName;
    public string EngineType => "ovgenai";

    public OVGenAIBackend(
        OVGenAIOptions options,
        OVGenAIProcessSupervisor supervisor,
        HttpClient httpClient,
        ILogger<OVGenAIBackend> logger)
    {
        _options = options;
        _supervisor = supervisor;
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<EngineStatus> GetStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineStatus(EngineId, _supervisor.IsRunning ? "running" : "stopped", _supervisor.IsRunning, _supervisor.IsRunning ? DateTimeOffset.UtcNow : null));

    public async Task<EngineHealth> GetHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"http://127.0.0.1:{_options.Port}/health", ct);
            return new EngineHealth(EngineId, response.IsSuccessStatusCode, null);
        }
        catch
        {
            return new EngineHealth(EngineId, false, "OVGenAI backend not reachable.");
        }
    }

    public Task<EngineCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineCapabilities
        {
            SupportsMultiModel = false,
            SupportsHotSwap = true,
            SupportsUnloadModel = true,
            SupportsOpenAiApi = true,
            SupportsStreaming = true,
            SupportsUpdate = false,
            SupportsRuntimeUpdate = false,
            SupportsMetrics = false
        });

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_supervisor.IsRunning) return;
        var started = _supervisor.Start(_options);
        if (!started) throw new Exception("Failed to start OVGenAI backend.");
        await Task.Delay(2000, ct);
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _supervisor.Stop();
        return Task.CompletedTask;
    }

    public async Task RestartAsync(CancellationToken ct = default)
    {
        _supervisor.Stop();
        await Task.Delay(500, ct);
        var started = _supervisor.Start(_options);
        if (!started) throw new Exception("Failed to restart OVGenAI backend.");
        await Task.Delay(2000, ct);
    }

    public Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ModelInfo>>(new List<ModelInfo>
        {
            new ModelInfo(_options.ModelName, _options.ModelName, EngineId, _options.ModelName, _options.ModelPath)
        });

    public async Task<ModelSwitchResult> SwitchModelAsync(ModelSwitchRequest request, CancellationToken ct = default)
    {
        var newPath = request.TargetEngineModelName ?? _options.ModelPath;
        if (_options.ModelName == request.ModelId && _options.ModelPath == newPath && _supervisor.IsRunning)
        {
            _logger.LogInformation("Model '{ModelId}' is already loaded on backend '{EngineId}'. Skipping restart.", request.ModelId, EngineId);
            return new ModelSwitchResult(true, null);
        }

        _supervisor.Stop();
        _options.ModelName = request.ModelId;
        _options.ModelPath = newPath;
        var started = _supervisor.Start(_options);
        if (!started) return new ModelSwitchResult(false, "Failed to switch model.");

        // Wait until backend is healthy (max 5 min)
        _logger.LogInformation("Waiting for backend '{EngineId}' to become healthy after switch...", EngineId);
        for (int i = 0; i < 300; i++)
        {
            await Task.Delay(1000, ct);
            var health = await GetHealthAsync(ct);
            if (health.Reachable)
            {
                _logger.LogInformation("Backend '{EngineId}' is healthy.", EngineId);
                return new ModelSwitchResult(true, null);
            }
        }
        return new ModelSwitchResult(false, "Backend did not become healthy within 5 minutes after switch.");
    }

    public async Task<OpenAiModelListResponse> ListOpenAiModelsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"http://127.0.0.1:{_options.Port}/v1/models", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OpenAiModelListResponse>(OpenAiJsonOptions.Default, ct)
            ?? throw new InvalidOperationException("Invalid response from OVGenAI.");
    }

    public async Task<OpenAiChatCompletionResponse> CreateChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"http://127.0.0.1:{_options.Port}/v1/chat/completions",
            request,
            OpenAiJsonOptions.Default,
            ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OpenAiChatCompletionResponse>(OpenAiJsonOptions.Default, ct)
            ?? throw new InvalidOperationException("Invalid response from OVGenAI.");
    }

    public async IAsyncEnumerable<OpenAiChatCompletionChunk> StreamChatCompletionAsync(OpenAiChatCompletionRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_options.Port}/v1/chat/completions");
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

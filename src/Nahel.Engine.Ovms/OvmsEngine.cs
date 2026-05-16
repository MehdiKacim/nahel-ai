using Microsoft.Extensions.Logging;
using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Engine.Ovms;

public sealed class OvmsEngine : IEngine, IEngineInstaller, IEngineUpdater
{
    private readonly OvmsOptions _options;
    private readonly OvmsProcessSupervisor _supervisor;
    private readonly OvmsHealthClient _healthClient;
    private readonly OvmsConfigWriter _configWriter;
    private readonly OvmsModelRegistry _modelRegistry;
    private readonly OvmsModelSwitcher _modelSwitcher;
    private readonly OvmsVersionService _versionService;
    private readonly ILogger<OvmsEngine> _logger;

    public string EngineId => _options.EngineId;
    public string DisplayName => _options.DisplayName;
    public string EngineType => "ovms";

    public OvmsEngine(
        OvmsOptions options,
        OvmsProcessSupervisor supervisor,
        OvmsHealthClient healthClient,
        OvmsConfigWriter configWriter,
        OvmsModelRegistry modelRegistry,
        OvmsModelSwitcher modelSwitcher,
        OvmsVersionService versionService,
        ILogger<OvmsEngine> logger)
    {
        _options = options;
        _supervisor = supervisor;
        _healthClient = healthClient;
        _configWriter = configWriter;
        _modelRegistry = modelRegistry;
        _modelSwitcher = modelSwitcher;
        _versionService = versionService;
        _logger = logger;
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
        => _modelRegistry.GetModelsAsync(ct);

    public async Task<ModelSwitchResult> SwitchModelAsync(ModelSwitchRequest request, CancellationToken ct = default)
        => await _modelSwitcher.SwitchAsync(_options, request, ct);

    public Task<EngineInstallStatus> GetInstallStatusAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineInstallStatus(EngineId, false, null));

    public Task<EngineInstallResult> InstallAsync(EngineInstallRequest request, CancellationToken ct = default)
        => Task.FromResult(new EngineInstallResult(false, "OVMS auto-install not implemented."));

    public Task<EngineVerifyResult> VerifyAsync(CancellationToken ct = default)
        => Task.FromResult(new EngineVerifyResult(false, "Verification not implemented."));

    public Task<EngineVersionInfo> GetVersionAsync(CancellationToken ct = default)
        => _versionService.GetVersionAsync(ct);

    public Task<EngineUpdateResult> UpdateEngineAsync(EngineUpdateRequest request, CancellationToken ct = default)
        => Task.FromResult(new EngineUpdateResult(false, "Engine update not implemented."));

    public Task<EngineUpdateResult> UpdateRuntimeAsync(EngineUpdateRequest request, CancellationToken ct = default)
        => Task.FromResult(new EngineUpdateResult(false, "Runtime update not implemented."));
}

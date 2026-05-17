using Microsoft.Extensions.Logging;
using Nahel.SDK.Models;

namespace Nahel.Engine.Ovms;

public sealed class OvmsModelSwitcher
{
    private readonly OvmsConfigWriter _configWriter;
    private readonly OvmsProcessSupervisor _supervisor;
    private readonly OvmsHealthClient _healthClient;
    private readonly ILogger<OvmsModelSwitcher> _logger;

    public OvmsModelSwitcher(OvmsConfigWriter configWriter, OvmsProcessSupervisor supervisor, OvmsHealthClient healthClient, ILogger<OvmsModelSwitcher> logger)
    {
        _configWriter = configWriter;
        _supervisor = supervisor;
        _healthClient = healthClient;
        _logger = logger;
    }

    public async Task<ModelSwitchResult> SwitchAsync(OvmsOptions options, ModelSwitchRequest request, CancellationToken ct = default)
    {
        try
        {
            var modelConfig = new OvmsModelConfig(
                request.ModelId,
                request.TargetEngineModelName ?? request.ModelId,
                request.TargetEngineModelName ?? request.ModelId,
                null);

            await _configWriter.WriteConfigAsync(options.ConfigPath, new[] { modelConfig }, ct);

            if (_supervisor.IsRunning)
            {
                await _supervisor.RestartAsync(options, ct);
                var ready = await _configWriter.WaitReadinessAsync("127.0.0.1", options.RestPort, TimeSpan.FromSeconds(60), ct);
                if (!ready)
                    return new ModelSwitchResult(false, "OVMS did not become ready after switch.");
            }

            return new ModelSwitchResult(true, "Model switched successfully.");
        }
        catch (Exception ex)
        {
            return new ModelSwitchResult(false, ex.Message);
        }
    }
}

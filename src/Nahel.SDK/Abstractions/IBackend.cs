using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IBackend
{
    string EngineId { get; }
    string DisplayName { get; }
    string EngineType { get; }

    Task<EngineStatus> GetStatusAsync(CancellationToken ct = default);
    Task<EngineHealth> GetHealthAsync(CancellationToken ct = default);
    Task<EngineCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task RestartAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct = default);
    Task<ModelSwitchResult> SwitchModelAsync(ModelSwitchRequest request, CancellationToken ct = default);
}

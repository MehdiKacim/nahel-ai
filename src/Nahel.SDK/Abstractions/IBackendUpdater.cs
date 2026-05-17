using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IBackendUpdater
{
    Task<EngineVersionInfo> GetVersionAsync(CancellationToken ct = default);
    Task<EngineUpdateResult> UpdateEngineAsync(EngineUpdateRequest request, CancellationToken ct = default);
    Task<EngineUpdateResult> UpdateRuntimeAsync(EngineUpdateRequest request, CancellationToken ct = default);
}

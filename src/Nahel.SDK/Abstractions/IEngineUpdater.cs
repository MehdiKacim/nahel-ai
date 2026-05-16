using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IEngineUpdater
{
    Task<EngineVersionInfo> GetVersionAsync(CancellationToken ct = default);
    Task<EngineUpdateResult> UpdateEngineAsync(EngineUpdateRequest request, CancellationToken ct = default);
    Task<EngineUpdateResult> UpdateRuntimeAsync(EngineUpdateRequest request, CancellationToken ct = default);
}

using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IEngineCapabilitiesProvider
{
    Task<EngineCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
}

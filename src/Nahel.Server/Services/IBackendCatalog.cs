using Nahel.SDK.Abstractions;

namespace Nahel.Server.Services;

public interface IBackendCatalog
{
    IReadOnlyList<IBackend> GetBackends();
    IBackend? GetBackend(string engineId);
    void RegisterBackend(IBackend engine);
    bool UnregisterBackend(string engineId);
}

using Nahel.SDK.Abstractions;

namespace Nahel.Server.Services;

public sealed class BackendCatalog : IBackendCatalog
{
    private readonly List<IBackend> _engines = new();

    public IReadOnlyList<IBackend> GetBackends() => _engines;

    public IBackend? GetBackend(string engineId) => _engines.FirstOrDefault(e => e.EngineId == engineId);

    public void RegisterBackend(IBackend engine)
    {
        if (!_engines.Any(e => e.EngineId == engine.EngineId))
            _engines.Add(engine);
    }

    public bool UnregisterBackend(string engineId)
    {
        var engine = _engines.FirstOrDefault(e => e.EngineId == engineId);
        if (engine == null) return false;
        _engines.Remove(engine);
        return true;
    }
}

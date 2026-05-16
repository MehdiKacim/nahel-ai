using Nahel.SDK.Abstractions;

namespace Nahel.Server.Services;

public sealed class EngineCatalog : IEngineCatalog
{
    private readonly List<IEngine> _engines = new();

    public IReadOnlyList<IEngine> GetEngines() => _engines;

    public IEngine? GetEngine(string engineId) => _engines.FirstOrDefault(e => e.EngineId == engineId);

    public void RegisterEngine(IEngine engine)
    {
        if (!_engines.Any(e => e.EngineId == engine.EngineId))
            _engines.Add(engine);
    }
}

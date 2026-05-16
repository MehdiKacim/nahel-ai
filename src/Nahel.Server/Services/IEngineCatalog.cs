using Nahel.SDK.Abstractions;

namespace Nahel.Server.Services;

public interface IEngineCatalog
{
    IReadOnlyList<IEngine> GetEngines();
    IEngine? GetEngine(string engineId);
    void RegisterEngine(IEngine engine);
}

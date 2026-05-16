using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class ModelRouter : IModelRouter
{
    private readonly Dictionary<string, (string engineId, string? engineModelName)> _map = new();

    public void Register(string publicModelId, string engineId, string? engineModelName = null)
        => _map[publicModelId] = (engineId, engineModelName);

    public (string engineId, string? engineModelName)? ResolveModel(string publicModelId)
        => _map.TryGetValue(publicModelId, out var v) ? v : null;

    public IReadOnlyList<ModelInfo> ListModels()
        => _map.Select(m => new ModelInfo(m.Key, m.Key, m.Value.engineId, m.Value.engineModelName, null)).ToList();
}

using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IModelRouter
{
    void Register(string publicModelId, string engineId, string? engineModelName = null);
    bool Unregister(string publicModelId);
    (string engineId, string? engineModelName)? ResolveModel(string publicModelId);
    IReadOnlyList<ModelInfo> ListModels();
}

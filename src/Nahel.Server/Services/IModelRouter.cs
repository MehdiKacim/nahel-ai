using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IModelRouter
{
    (string engineId, string? engineModelName)? ResolveModel(string publicModelId);
    IReadOnlyList<ModelInfo> ListModels();
}

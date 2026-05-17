using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Engine.Ovms;

public sealed class OvmsModelRegistry : IModelRegistry
{
    private readonly List<ModelInfo> _models = new();

    public Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ModelInfo>>(_models.ToList());

    public Task<ModelInfo?> GetModelAsync(string modelId, CancellationToken ct = default)
        => Task.FromResult(_models.FirstOrDefault(m => m.ModelId == modelId));

    public Task RegisterModelAsync(ModelRegistrationRequest request, CancellationToken ct = default)
    {
        _models.Add(new ModelInfo(request.ModelId, request.DisplayName, request.EngineId, request.EngineModelName, request.ModelPath));
        return Task.CompletedTask;
    }

    public Task RemoveModelAsync(string modelId, CancellationToken ct = default)
    {
        _models.RemoveAll(m => m.ModelId == modelId);
        return Task.CompletedTask;
    }
}

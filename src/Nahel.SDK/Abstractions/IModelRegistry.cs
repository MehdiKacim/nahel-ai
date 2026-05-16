using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IModelRegistry
{
    Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken ct = default);
    Task<ModelInfo?> GetModelAsync(string modelId, CancellationToken ct = default);
    Task RegisterModelAsync(ModelRegistrationRequest request, CancellationToken ct = default);
    Task RemoveModelAsync(string modelId, CancellationToken ct = default);
}

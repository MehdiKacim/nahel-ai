using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class OpenAiRouter : IOpenAiRouter
{
    private readonly IModelRouter _modelRouter;
    private readonly IBackendCatalog _catalog;

    public OpenAiRouter(IModelRouter modelRouter, IBackendCatalog catalog)
    {
        _modelRouter = modelRouter;
        _catalog = catalog;
    }

    public async Task<OpenAiChatCompletionResponse> RouteChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default)
    {
        var resolved = _modelRouter.ResolveModel(request.Model);
        if (resolved == null) throw new Exception($"Model '{request.Model}' not found.");

        var engine = _catalog.GetBackend(resolved.Value.engineId);
        if (engine == null) throw new Exception($"Engine '{resolved.Value.engineId}' not found.");
        if (engine is not IOpenAiBackend openAiEngine) throw new Exception($"Engine '{resolved.Value.engineId}' does not support OpenAI API.");

        return await openAiEngine.CreateChatCompletionAsync(request, ct);
    }

    public IAsyncEnumerable<OpenAiChatCompletionChunk> RouteStreamChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct)
    {
        var resolved = _modelRouter.ResolveModel(request.Model);
        if (resolved == null) throw new Exception($"Model '{request.Model}' not found.");

        var engine = _catalog.GetBackend(resolved.Value.engineId);
        if (engine == null) throw new Exception($"Engine '{resolved.Value.engineId}' not found.");
        if (engine is not IOpenAiBackend openAiEngine) throw new Exception($"Engine '{resolved.Value.engineId}' does not support OpenAI API.");

        return openAiEngine.StreamChatCompletionAsync(request, ct);
    }
}

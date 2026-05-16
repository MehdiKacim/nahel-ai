using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IOpenAiRouter
{
    Task<OpenAiChatCompletionResponse> RouteChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default);
    IAsyncEnumerable<OpenAiChatCompletionChunk> RouteStreamChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct);
}

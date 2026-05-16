using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IOpenAiCompatibleEngine
{
    Task<OpenAiModelListResponse> ListOpenAiModelsAsync(CancellationToken ct = default);
    Task<OpenAiChatCompletionResponse> CreateChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default);
    IAsyncEnumerable<OpenAiChatCompletionChunk> StreamChatCompletionAsync(OpenAiChatCompletionRequest request, CancellationToken ct = default);
}

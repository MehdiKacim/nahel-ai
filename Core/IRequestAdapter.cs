using System.Text.Json;

namespace OllamaBridge.Core;

public interface IRequestAdapter
{
    Task<object> ToBackendFormatAsync(JsonElement ollamaRequest, string modelRealName, string backendType);
    IAsyncEnumerable<string> FromBackendStreamAsync(Stream backendStream, string modelName, CancellationToken ct);
}

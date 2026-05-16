namespace Nahel.SDK.Models;
public sealed record OpenAiModelListResponse(string Object, IReadOnlyList<OpenAiModelInfo> Data);

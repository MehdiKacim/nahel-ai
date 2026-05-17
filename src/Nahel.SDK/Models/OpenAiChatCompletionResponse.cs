namespace Nahel.SDK.Models;

public sealed record OpenAiChatCompletionResponse(
    string? Id = null,
    string? Object = null,
    long? Created = null,
    string? Model = null,
    IReadOnlyList<OpenAiChatChoice>? Choices = null,
    OpenAiUsage? Usage = null);

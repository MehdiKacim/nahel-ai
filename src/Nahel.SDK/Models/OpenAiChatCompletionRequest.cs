namespace Nahel.SDK.Models;

public sealed record OpenAiChatCompletionRequest(
    string Model,
    IReadOnlyList<OpenAiChatMessage> Messages,
    bool? Stream = false,
    double? Temperature = null,
    int? MaxTokens = null);

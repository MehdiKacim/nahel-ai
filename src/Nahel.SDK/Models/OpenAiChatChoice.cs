namespace Nahel.SDK.Models;

public sealed record OpenAiChatChoice(
    int Index,
    object? Message = null,
    object? Delta = null,
    string? FinishReason = null,
    object? Logprobs = null);

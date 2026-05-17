namespace Nahel.SDK.Models;

public sealed record OpenAiChatMessage(
    string Role,
    object Content,
    object? ToolCalls = null);

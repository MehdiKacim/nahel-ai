namespace Nahel.SDK.Models;
public sealed record OpenAiChatCompletionResponse(string Id, string Object, long Created, string Model, IReadOnlyList<OpenAiChatChoice> Choices, OpenAiUsage? Usage);

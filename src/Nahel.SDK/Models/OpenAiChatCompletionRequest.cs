namespace Nahel.SDK.Models;
public sealed record OpenAiChatCompletionRequest(string Model, IReadOnlyList<OpenAiChatMessage> Messages, bool Stream, double? Temperature, int? MaxTokens);

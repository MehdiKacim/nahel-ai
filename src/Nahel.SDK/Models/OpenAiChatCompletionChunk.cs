namespace Nahel.SDK.Models;
public sealed record OpenAiChatCompletionChunk(string Id, string Object, long Created, string Model, IReadOnlyList<OpenAiChatChoice> Choices);

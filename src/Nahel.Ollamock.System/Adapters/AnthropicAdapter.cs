using System.Text.Json;

namespace Nahel.Ollamock.System.Adapters;

public sealed class AnthropicAdapter
{
    public Task<object> ToOpenAiFormatAsync(JsonElement anthropicRequest, string modelRealName)
    {
        var messages = new List<object>();

        if (anthropicRequest.TryGetProperty("system", out var system))
        {
            messages.Add(new { role = "system", content = system.GetString() });
        }

        if (anthropicRequest.TryGetProperty("messages", out var msgs))
        {
            foreach (var msg in msgs.EnumerateArray())
            {
                messages.Add(new
                {
                    role = msg.GetProperty("role").GetString(),
                    content = msg.GetProperty("content").GetString()
                });
            }
        }

        var maxTokens = anthropicRequest.TryGetProperty("max_tokens", out var max) ? max.GetInt32() : 4096;
        var temperature = anthropicRequest.TryGetProperty("temperature", out var temp) ? temp.GetDouble() : 0.7;

        return Task.FromResult<object>(new
        {
            model = modelRealName,
            messages,
            max_tokens = maxTokens,
            temperature,
            stream = true
        });
    }

    public Task<object> FromOpenAiFormatAsync(JsonElement openAiResponse, string modelName)
    {
        var content = "";
        if (openAiResponse.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message))
            {
                content = message.GetProperty("content").GetString() ?? "";
            }
            else if (first.TryGetProperty("delta", out var delta))
            {
                content = delta.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
            }
        }

        var inputTokens = 0;
        var outputTokens = 0;
        if (openAiResponse.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
            outputTokens = usage.TryGetProperty("completion_tokens", out var ct2) ? ct2.GetInt32() : 0;
        }

        return Task.FromResult<object>(new
        {
            id = $"msg_{Guid.NewGuid().ToString("N")[..24]}",
            type = "message",
            role = "assistant",
            model = modelName,
            content = new[] { new { type = "text", text = content } },
            stop_reason = "end_turn",
            usage = new { input_tokens = inputTokens, output_tokens = outputTokens }
        });
    }
}

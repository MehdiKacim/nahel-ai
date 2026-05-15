using System.Text.Json;

namespace Ollamock.Service.Bridge;

public class OpenAiAdapter : IRequestAdapter
{
    public Task<object> ToProviderFormatAsync(JsonElement ollamaRequest, string modelRealName, string providerType)
    {
        if (providerType == "embed")
        {
            var input = ollamaRequest.TryGetProperty("input", out var inp)
                ? inp.GetString() ?? inp.ToString()
                : ollamaRequest.GetProperty("prompt").GetString() ?? "";

            return Task.FromResult<object>(new { model = modelRealName, input });
        }

        var messages = new List<object>();
        if (ollamaRequest.TryGetProperty("system", out var sys))
            messages.Add(new { role = "system", content = sys.GetString() });

        var prompt = ollamaRequest.GetProperty("prompt").GetString() ?? "";

        if (ollamaRequest.TryGetProperty("images", out var imgs) && imgs.GetArrayLength() > 0)
        {
            var content = new List<object> { new { type = "text", text = prompt } };
            foreach (var img in imgs.EnumerateArray())
                content.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{img.GetString()}" } });
            messages.Add(new { role = "user", content });
        }
        else
        {
            messages.Add(new { role = "user", content = prompt });
        }

        var options = ollamaRequest.TryGetProperty("options", out var opts) ? opts : new JsonElement();

        return Task.FromResult<object>(new
        {
            model = modelRealName,
            messages,
            stream = true,
            temperature = options.TryGetProperty("temperature", out var t) ? t.GetDouble() : 0.7,
            max_tokens = options.TryGetProperty("num_predict", out var n) ? n.GetInt32() : 4096
        });
    }

    public async IAsyncEnumerable<string> FromProviderStreamAsync(Stream providerStream, string modelName, CancellationToken ct)
    {
        using var reader = new StreamReader(providerStream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]") break;

            try
            {
                var chunk = JsonSerializer.Deserialize<JsonElement>(data);
                var delta = chunk.GetProperty("choices")[0].GetProperty("delta");
                var content = delta.TryGetProperty("content", out var c) ? c.GetString() : "";
                var finish = chunk.GetProperty("choices")[0].TryGetProperty("finish_reason", out var f)
                    && f.ValueKind != JsonValueKind.Null;

                yield return JsonSerializer.Serialize(new
                {
                    model = modelName,
                    created_at = DateTime.UtcNow.ToString("o"),
                    response = content,
                    done = finish
                });
            }
            catch { }
        }
        yield return JsonSerializer.Serialize(new { model = modelName, done = true });
    }
}

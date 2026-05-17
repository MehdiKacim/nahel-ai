using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Nahel.Cli.Commands;

public sealed class SseCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: nahel sse <model> \"message\"");
            return 1;
        }

        var modelId = args[0];
        var message = args[1];

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        var payload = new
        {
            model = modelId,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = message }
            },
            stream = true,
            max_tokens = 512,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11435/v1/chat/completions") { Content = content };

        var totalSw = Stopwatch.StartNew();
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Request failed: HTTP {(int)response.StatusCode}");
            return 1;
        }

        var ttftSw = Stopwatch.StartNew();
        long ttftMs = 0;
        long generationMs = 0;
        int charsGenerated = 0;
        bool firstTokenReceived = false;

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentProp))
                    {
                        var text = contentProp.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (!firstTokenReceived)
                            {
                                ttftSw.Stop();
                                ttftMs = ttftSw.ElapsedMilliseconds;
                                firstTokenReceived = true;
                            }
                            charsGenerated += text.Length;
                            Console.Write(text);
                        }
                    }
                }
            }
            catch { /* ignore malformed chunks */ }
        }

        totalSw.Stop();
        var totalMs = totalSw.ElapsedMilliseconds;
        generationMs = totalMs - ttftMs;
        if (generationMs < 0) generationMs = 0;

        var estimatedTokens = (int)Math.Ceiling(charsGenerated / 4.0);
        var genTps = generationMs > 0 ? estimatedTokens / (generationMs / 1000.0) : 0;
        var e2eTps = totalMs > 0 ? estimatedTokens / (totalMs / 1000.0) : 0;

        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine($"  TTFT                {ttftMs} ms");
        Console.WriteLine($"  Generation duration {generationMs} ms");
        Console.WriteLine($"  Total duration      {totalMs} ms");
        Console.WriteLine($"  Chars generated     {charsGenerated}");
        Console.WriteLine($"  Est. tokens         {estimatedTokens} (~4 chars/tok)");
        Console.WriteLine($"  Generation speed    {genTps:F2} tok/s");
        Console.WriteLine($"  End-to-end speed    {e2eTps:F2} tok/s");

        return 0;
    }
}

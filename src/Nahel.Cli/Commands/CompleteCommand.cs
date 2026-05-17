using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Nahel.Cli.Commands;

public sealed class CompleteCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: nahel complete <model> \"message\"");
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
            stream = false,
            max_tokens = 512,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var sw = Stopwatch.StartNew();
        var response = await http.PostAsync("http://127.0.0.1:11435/v1/chat/completions", content);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Request failed: HTTP {(int)response.StatusCode}");
            return 1;
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var contentProp))
            {
                Console.WriteLine(contentProp.GetString());
            }
        }

        int completionTokens = 0;
        int totalTokens = 0;
        if (doc.RootElement.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("completion_tokens", out var ct))
                completionTokens = ct.GetInt32();
            if (usage.TryGetProperty("total_tokens", out var tt))
                totalTokens = tt.GetInt32();
        }

        var durationMs = sw.ElapsedMilliseconds;
        var tps = durationMs > 0 ? completionTokens / (durationMs / 1000.0) : 0;

        Console.WriteLine();
        Console.WriteLine($"  Duration          {durationMs} ms");
        Console.WriteLine($"  Completion tokens {completionTokens}");
        Console.WriteLine($"  Total tokens      {totalTokens}");
        Console.WriteLine($"  End-to-end speed  {tps:F2} tok/s");

        return 0;
    }
}

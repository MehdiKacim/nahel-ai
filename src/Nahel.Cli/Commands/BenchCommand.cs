using System.Diagnostics;
using System.Text.Json;

namespace Nahel.Cli.Commands;

public sealed class BenchCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        using var client = new NahelApiClient();

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: nahel bench <model> [--token <n>]");
            return 1;
        }

        var modelId = args[0];
        int? maxTokens = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] is "--token" or "--tokens" or "-t" && i + 1 < args.Length && int.TryParse(args[i + 1], out var t))
            {
                maxTokens = t;
                i++;
            }
        }

        Console.WriteLine($"Benchmarking '{modelId}'{(maxTokens.HasValue ? $" (max_tokens={maxTokens})" : "")}...");

        var sw = Stopwatch.StartNew();
        var result = await client.PostAsync("/bench", new { modelId, maxTokens });
        sw.Stop();

        if (result == null)
        {
            Console.WriteLine("Benchmark failed. Is the server running?");
            return 1;
        }

        if (result.RootElement.TryGetProperty("error", out var err))
        {
            Console.WriteLine($"Error: {err.GetString()}");
            return 1;
        }

        var r = result.RootElement;
        var duration = r.TryGetProperty("duration_ms", out var d) ? d.GetInt64() : 0;
        var completionTokens = r.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
        var totalTokens = r.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;
        var tps = r.TryGetProperty("tokens_per_second", out var tp) ? tp.GetDouble() : 0.0;
        var preview = r.TryGetProperty("preview", out var p) ? p.GetString() ?? "" : "";

        Console.WriteLine();
        Console.WriteLine($"  Model           {modelId}");
        Console.WriteLine($"  Duration        {duration} ms");
        Console.WriteLine($"  Completion      {completionTokens} tokens");
        Console.WriteLine($"  Total           {totalTokens} tokens");
        Console.WriteLine($"  End-to-end speed  {tps:F2} tok/s");
        if (!string.IsNullOrEmpty(preview))
            Console.WriteLine($"  Preview         {preview}");

        return 0;
    }
}

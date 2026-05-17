using System.Text.Json;

namespace Nahel.Cli.Commands;

public sealed class BenchCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        using var client = new NahelApiClient();

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: nahel bench <model>");
            return 1;
        }
        var modelId = args[0];
        Console.WriteLine($"Running benchmark for '{modelId}'...");

        var result = await client.PostAsync("/bench", new { modelId });
        if (result == null)
        {
            Console.WriteLine("Benchmark failed. Is the server running?");
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}

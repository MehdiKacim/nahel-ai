using System.Text.Json;

namespace Nahel.Cli.Commands;

public sealed class MetricsCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        using var client = new NahelApiClient();

        var result = await client.GetAsync("/metrics");
        if (result == null)
        {
            Console.WriteLine("Failed to fetch metrics. Is the server running?");
            return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}

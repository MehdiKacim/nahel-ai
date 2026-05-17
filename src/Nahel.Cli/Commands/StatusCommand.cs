using System.Text.Json;

namespace Nahel.Cli.Commands;

public sealed class StatusCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        using var client = new NahelApiClient();
        var doc = await client.GetAsync("/api/status");
        if (doc == null)
        {
            Console.WriteLine("Nahel server is not running. Start it with: nahel start");
            return 1;
        }

        var root = doc.RootElement;
        if (root.TryGetProperty("version", out var version))
            Console.WriteLine($"Nahel {version.GetString()}");
        else
            Console.WriteLine("Nahel server");

        Console.WriteLine();

        if (root.TryGetProperty("backends", out var backends))
        {
            Console.WriteLine("Backends:");
            foreach (var b in backends.EnumerateArray())
            {
                var id = b.TryGetProperty("engine_id", out var pId) ? pId.GetString() : "?";
                var type = b.TryGetProperty("engine_type", out var pType) ? pType.GetString() : "?";
                var state = b.TryGetProperty("state", out var pState) ? pState.GetString() : "?";
                Console.WriteLine($"  [{type}] {id} -> {state}");
            }
        }
        else
        {
            Console.WriteLine("Backends: (none)");
        }

        if (root.TryGetProperty("models", out var models))
        {
            Console.WriteLine("Models:");
            foreach (var m in models.EnumerateArray())
            {
                var id = m.TryGetProperty("model_id", out var pId) ? pId.GetString() : "?";
                var backend = m.TryGetProperty("backend_id", out var pBe) ? pBe.GetString() : "?";
                Console.WriteLine($"  {id} -> {backend}");
            }
        }
        else
        {
            Console.WriteLine("Models: (none)");
        }

        return 0;
    }
}

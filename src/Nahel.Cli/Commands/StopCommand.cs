using Nahel.Cli;

namespace Nahel.Cli.Commands;

public sealed class StopCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("To stop the Nahel server, press Ctrl+C in the terminal where it is running.");
            Console.WriteLine("Or use: taskkill /F /IM Nahel.Cli.exe");
            return 0;
        }

        // Stop a specific backend via API
        var backendId = args[0];
        using var client = new NahelApiClient();
        var result = await client.PostAsync($"/engine/{backendId}/stop", new { });
        if (result == null)
        {
            Console.WriteLine($"Failed to stop backend '{backendId}'. Is the server running?");
            return 1;
        }
        Console.WriteLine($"Backend '{backendId}' stop requested.");
        return 0;
    }
}

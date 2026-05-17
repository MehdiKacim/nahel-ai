using System.Diagnostics;

namespace Nahel.Cli.Commands;

public sealed class OpenCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        using var client = new NahelApiClient();
        var status = await client.GetAsync("/api/status");
        if (status == null)
        {
            Console.WriteLine("Nahel server is not running. Start it with: nahel start");
            return 1;
        }

        var url = "http://127.0.0.1:11435/dashboard";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { Console.WriteLine(url); }
        return 0;
    }
}

using System.Diagnostics;

namespace Nahel.Cli.Commands;

public sealed class OpenCommand : ICommand
{
    public Task<int> ExecuteAsync(string[] args)
    {
        var url = "http://127.0.0.1:11435/dashboard";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { Console.WriteLine(url); }
        return Task.FromResult(0);
    }
}

namespace Nahel.Cli.Commands;

public sealed class StatusCommand : ICommand
{
    public Task<int> ExecuteAsync(string[] args)
    {
        Console.WriteLine("Nahel status: server not running (or check http://127.0.0.1:11435/health)");
        return Task.FromResult(0);
    }
}

namespace Nahel.Cli.Commands;

public sealed class StopCommand : ICommand
{
    public Task<int> ExecuteAsync(string[] args)
    {
        Console.WriteLine("Stop: use Ctrl+C to stop the running server.");
        return Task.FromResult(0);
    }
}

namespace Nahel.Cli.Commands;

public sealed class EnginesCommand : ICommand
{
    public Task<int> ExecuteAsync(string[] args)
    {
        Console.WriteLine("Engines: use API GET /engine");
        return Task.FromResult(0);
    }
}

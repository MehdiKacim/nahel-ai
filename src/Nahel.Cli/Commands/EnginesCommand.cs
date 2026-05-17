namespace Nahel.Cli.Commands;

public sealed class EnginesCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        // Engines are now backends — delegate to status command
        return await new StatusCommand().ExecuteAsync(args);
    }
}

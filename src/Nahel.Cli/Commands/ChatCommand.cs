namespace Nahel.Cli.Commands;

public sealed class ChatCommand : ICommand
{
    public Task<int> ExecuteAsync(string[] args) => new SseCommand().ExecuteAsync(args);
}

namespace Nahel.Cli.Commands;

public sealed class ModelsCommand : ICommand
{
    public Task<int> ExecuteAsync(string[] args)
    {
        Console.WriteLine("Models: use API GET /v1/models or GET /engine/{id}/models");
        return Task.FromResult(0);
    }
}

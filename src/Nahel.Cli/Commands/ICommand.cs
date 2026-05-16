namespace Nahel.Cli.Commands;

public interface ICommand
{
    Task<int> ExecuteAsync(string[] args);
}

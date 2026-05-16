using Nahel.Cli.Commands;

namespace Nahel.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0) { Console.WriteLine("Usage: nahel <command> [options]"); return 1; }
        var command = args[0].ToLowerInvariant();
        var remaining = args.Skip(1).ToArray();
        return command switch
        {
            "start" => await new StartCommand().ExecuteAsync(remaining),
            "status" => await new StatusCommand().ExecuteAsync(remaining),
            "stop" => await new StopCommand().ExecuteAsync(remaining),
            "open" => await new OpenCommand().ExecuteAsync(remaining),
            "models" => await new ModelsCommand().ExecuteAsync(remaining),
            "engines" => await new EnginesCommand().ExecuteAsync(remaining),
            _ => await new DefaultCommand(command, remaining).ExecuteAsync(remaining)
        };
    }
}

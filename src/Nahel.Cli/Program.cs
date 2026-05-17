using Nahel.Cli.Commands;

namespace Nahel.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

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
            "backends" => await new EnginesCommand().ExecuteAsync(remaining),
            "bench" => await new BenchCommand().ExecuteAsync(remaining),
            "metrics" => await new MetricsCommand().ExecuteAsync(remaining),
            "run" => await new RunCommand().ExecuteAsync(remaining),
            "chat" => await new ChatCommand().ExecuteAsync(remaining),
            "complete" => await new CompleteCommand().ExecuteAsync(remaining),
            "sse" => await new SseCommand().ExecuteAsync(remaining),
            "list" => await new ModelsCommand().ExecuteAsync(new[] { "list" }),
            "ps" => await new StatusCommand().ExecuteAsync(remaining),
            _ => await new DefaultCommand(command, remaining).ExecuteAsync(remaining)
        };
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            Nahel CLI
            Node-based Agent Harmony and Execution Layer

            Usage:
              nahel <command> [options]

            Commands:
              start       Start the Nahel server
                          Options: --host, --port, --config, --lan, --no-dashboard
              status      Show server status
              stop        Stop the running server (advises Ctrl+C)
              open        Open dashboard in browser
              models      Manage models (list, add, download, switch, rm)
              backends    List backends hint
              bench       Run inference benchmark on a model
              metrics     Show hardware / backend metrics

            Ollama-like commands:
              start <tool>    Start a tool (codex, claude, ...)
              run <model>     Run a model
              stop <target>   Stop a backend or tool
              list            List models
              ps              List running processes

            Global options:
              -h, --help      Show this help message

            Examples:
              nahel start
              nahel start --lan --port 8080
              nahel run <model>
              nahel chat <model> "Hello"
              nahel complete <model> "Hello"
              nahel sse <model> "Hello"
              nahel status
              nahel bench <model>
              nahel metrics
            """);
    }
}

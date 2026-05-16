using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nahel.Ollamock.System.Commands;
using Nahel.Ollamock.System.Launchers;

namespace Nahel.Cli.Commands;

public sealed class DefaultCommand : ICommand
{
    private readonly string _command;
    private readonly string[] _args;

    public DefaultCommand(string command, string[] args) { _command = command; _args = args; }

    public async Task<int> ExecuteAsync(string[] args)
    {
        var parser = new OllamaCommandParser();
        var cmd = parser.Parse(new[] { _command }.Concat(_args).ToList());
        if (cmd.Type == OllamaCommandType.Unknown) { Console.WriteLine($"Unknown command: {_command}"); return 1; }

        // We need a minimal service provider to run the router
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Nahel.Ollamock.System.Backup.IConfigBackup, Nahel.Ollamock.System.Backup.ConfigBackup>();
        services.AddSingleton<ToolLauncherRegistry>();
        var sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<ToolLauncherRegistry>();
        var router = new OllamaCommandRouter(registry);
        var result = await router.ExecuteAsync(cmd);
        Console.WriteLine(result.Message);
        return result.Success ? 0 : 1;
    }
}

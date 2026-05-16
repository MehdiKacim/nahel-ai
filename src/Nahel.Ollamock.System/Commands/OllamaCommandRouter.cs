using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;
using Nahel.Ollamock.System.Launchers;

namespace Nahel.Ollamock.System.Commands;

public sealed class OllamaCommandRouter
{
    private readonly ToolLauncherRegistry _registry;

    public OllamaCommandRouter(ToolLauncherRegistry registry)
    {
        _registry = registry;
    }

    public async Task<OllamaCommandResult> ExecuteAsync(OllamaCommand command, CancellationToken ct = default)
    {
        switch (command.Type)
        {
            case OllamaCommandType.Start:
            case OllamaCommandType.Run:
                if (string.IsNullOrEmpty(command.Target))
                    return new OllamaCommandResult(false, "Target required.");

                var launcher = _registry.Get(command.Target);
                if (launcher == null)
                    return new OllamaCommandResult(false, $"Tool '{command.Target}' not found.");

                var launchRequest = new ToolLaunchRequest(launcher.ToolId, command.Target, null);
                var launchResult = await launcher.StartAsync(launchRequest, ct);
                return new OllamaCommandResult(launchResult.Success, launchResult.Message ?? "OK");

            case OllamaCommandType.Stop:
                if (string.IsNullOrEmpty(command.Target))
                    return new OllamaCommandResult(false, "Target required.");

                var stopLauncher = _registry.Get(command.Target);
                if (stopLauncher == null)
                    return new OllamaCommandResult(false, $"Tool '{command.Target}' not found.");

                var stopRequest = new ToolStopRequest(command.Target, false);
                var stopResult = await stopLauncher.StopAsync(stopRequest, ct);
                return new OllamaCommandResult(stopResult.Success, stopResult.Message ?? "Stopped");

            case OllamaCommandType.List:
            case OllamaCommandType.Ps:
                var tools = await _registry.ListToolsAsync(ct);
                var message = string.Join("\n", tools.Select(t => $"{t.ToolId}: {t.DisplayName}"));
                return new OllamaCommandResult(true, message);

            default:
                return new OllamaCommandResult(false, "Unknown command.");
        }
    }
}

public sealed record OllamaCommandResult(bool Success, string Message);

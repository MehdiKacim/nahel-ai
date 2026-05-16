namespace Nahel.Ollamock.System.Commands;

public sealed class OllamaCommandParser
{
    public OllamaCommand Parse(IReadOnlyList<string> args)
    {
        if (args == null || args.Count == 0)
        {
            return new OllamaCommand(OllamaCommandType.Unknown, null, Array.Empty<string>());
        }

        var type = args[0].ToLowerInvariant() switch
        {
            "start" => OllamaCommandType.Start,
            "run" => OllamaCommandType.Run,
            "stop" => OllamaCommandType.Stop,
            "list" => OllamaCommandType.List,
            "ps" => OllamaCommandType.Ps,
            "pull" => OllamaCommandType.Pull,
            "show" => OllamaCommandType.Show,
            "serve" => OllamaCommandType.Serve,
            _ => OllamaCommandType.Unknown
        };

        var target = args.Count > 1 ? args[1] : null;
        var remaining = args.Count > 2 ? args.Skip(2).ToArray() : Array.Empty<string>();

        return new OllamaCommand(type, target, remaining);
    }
}

namespace Nahel.Ollamock.System.Commands;

public enum OllamaCommandType { Start, Run, Stop, List, Ps, Pull, Show, Serve, Unknown }

public sealed record OllamaCommand(OllamaCommandType Type, string? Target, IReadOnlyList<string> Args);

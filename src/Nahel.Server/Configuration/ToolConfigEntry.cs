namespace Nahel.Server.Configuration;

public sealed class ToolConfigEntry
{
    public bool Enabled { get; init; } = true;
    public string ExecutablePath { get; init; } = "";
    public string Arguments { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public bool AutoStart { get; init; } = false;
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}

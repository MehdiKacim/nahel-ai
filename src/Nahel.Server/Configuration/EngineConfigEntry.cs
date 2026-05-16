using Nahel.SDK.Policies;

namespace Nahel.Server.Configuration;

public sealed class EngineConfigEntry
{
    public string Type { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public EngineAutoStartPolicy AutoStartPolicy { get; init; } = EngineAutoStartPolicy.ManualOnly;
    public string ExecutablePath { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public string ConfigPath { get; init; } = "";
    public int RestPort { get; init; } = 8000;
    public int GrpcPort { get; init; } = 9000;
    public int OpenAiProxyPort { get; init; } = 8080;
    public string OpenVinoVersion { get; init; } = "2026.1";
    public string VersionPolicy { get; init; } = "Official";
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}

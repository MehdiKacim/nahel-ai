namespace Nahel.Engine.Ovms;

public sealed record OvmsOptions
{
    public string EngineId { get; init; } = "ovms";
    public string DisplayName { get; init; } = "OpenVINO Model Server";
    public string ExecutablePath { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public string ConfigPath { get; init; } = "";
    public string? ModelName { get; init; }
    public string? ModelPath { get; init; }
    public int RestPort { get; init; } = 8000;
    public int GrpcPort { get; init; } = 9000;
    public int OpenAiProxyPort { get; init; } = 8080;
    public string OpenVinoVersion { get; init; } = "2026.1";
    public string VersionPolicy { get; init; } = "Official";
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}

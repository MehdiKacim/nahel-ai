namespace Nahel.Server.Configuration;

public sealed class NahelConfig
{
    public NahelHostOptions Nahel { get; init; } = new();
    public Dictionary<string, EngineConfigEntry> Engines { get; init; } = new();
    public Dictionary<string, ModelConfigEntry> Models { get; init; } = new();
    public Dictionary<string, ToolConfigEntry> Tools { get; init; } = new();
}

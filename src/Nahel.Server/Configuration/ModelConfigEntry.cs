using Nahel.SDK.Policies;

namespace Nahel.Server.Configuration;

public sealed class ModelConfigEntry
{
    public string EngineId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string EngineModelName { get; init; } = "";
    public string ModelPath { get; init; } = "";
    public int ContextSize { get; init; } = 4096;
    public ModelLoadPolicy LoadPolicy { get; init; } = ModelLoadPolicy.OnFirstRequest;
    public ModelUnloadPolicy UnloadPolicy { get; init; } = ModelUnloadPolicy.AfterIdleTimeout;
    public int IdleTimeoutSeconds { get; init; } = 900;
    public bool Preload { get; init; } = false;
    public bool Enabled { get; init; } = true;
}

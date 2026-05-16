namespace Nahel.SDK.Policies;
public sealed record ModelRuntimePolicy
{
    public ModelLoadPolicy LoadPolicy { get; init; } = ModelLoadPolicy.OnFirstRequest;
    public ModelUnloadPolicy UnloadPolicy { get; init; } = ModelUnloadPolicy.AfterIdleTimeout;
    public TimeSpan? IdleTimeout { get; init; } = TimeSpan.FromMinutes(15);
    public bool Preload { get; init; }
    public bool KeepWarm { get; init; }
    public bool AllowConcurrentRequests { get; init; } = true;
    public int MaxConcurrentRequests { get; init; } = 1;
    public int Priority { get; init; }
}

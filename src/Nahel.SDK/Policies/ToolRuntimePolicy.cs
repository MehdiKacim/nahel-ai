namespace Nahel.SDK.Policies;
public sealed record ToolRuntimePolicy
{
    public bool AutoStart { get; init; }
    public TimeSpan? IdleTimeout { get; init; }
}

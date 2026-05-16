namespace Nahel.SDK.Models;
public sealed record EngineStatus(string EngineId, string State, bool IsRunning, DateTimeOffset? LastStartedAt);

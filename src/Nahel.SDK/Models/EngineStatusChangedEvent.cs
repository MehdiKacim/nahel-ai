namespace Nahel.SDK.Models;
public sealed record EngineStatusChangedEvent(string EngineId, string PreviousState, string NewState, DateTimeOffset Timestamp);

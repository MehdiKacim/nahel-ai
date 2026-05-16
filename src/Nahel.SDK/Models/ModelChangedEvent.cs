namespace Nahel.SDK.Models;
public sealed record ModelChangedEvent(string EngineId, string ModelId, string Action, DateTimeOffset Timestamp);

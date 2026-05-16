namespace Nahel.SDK.Models;
public sealed record JobRequest(JobType Type, string? EngineId, string? ModelId, Dictionary<string, string>? Parameters);

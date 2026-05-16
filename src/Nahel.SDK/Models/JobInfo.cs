namespace Nahel.SDK.Models;
public sealed record JobInfo(string JobId, string? EngineId, JobType Type, JobStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? Error, IReadOnlyList<string> Logs);

namespace Nahel.SDK.Models;
public sealed record LogEvent(string Level, string Message, DateTimeOffset Timestamp, string? Source);

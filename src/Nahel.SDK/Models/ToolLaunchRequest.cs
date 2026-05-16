namespace Nahel.SDK.Models;
public sealed record ToolLaunchRequest(string ToolId, string? Model, Dictionary<string, string>? EnvironmentVariables);

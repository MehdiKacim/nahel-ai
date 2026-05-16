namespace Nahel.SDK.Errors;
public sealed record EngineError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string? EngineId { get; init; }
    public string? ModelId { get; init; }
    public Dictionary<string, string> Details { get; init; } = new();
}

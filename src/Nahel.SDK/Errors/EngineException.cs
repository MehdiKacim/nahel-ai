namespace Nahel.SDK.Errors;
public class EngineException : Exception
{
    public string? EngineId { get; }
    public EngineException(string message, string? engineId = null) : base(message) { EngineId = engineId; }
}

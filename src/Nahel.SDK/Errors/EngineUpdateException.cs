namespace Nahel.SDK.Errors;
public class EngineUpdateException : EngineException
{
    public EngineUpdateException(string engineId, string message) : base(message, engineId) { }
}

namespace Nahel.SDK.Errors;
public class EngineStopException : EngineException
{
    public EngineStopException(string engineId, string message) : base(message, engineId) { }
}

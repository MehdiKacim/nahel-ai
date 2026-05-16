namespace Nahel.SDK.Errors;
public class EngineStartException : EngineException
{
    public EngineStartException(string engineId, string message) : base(message, engineId) { }
}

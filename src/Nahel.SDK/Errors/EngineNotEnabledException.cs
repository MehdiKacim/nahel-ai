namespace Nahel.SDK.Errors;
public class EngineNotEnabledException : EngineException
{
    public EngineNotEnabledException(string engineId) : base($"Engine '{engineId}' is not enabled.", engineId) { }
}

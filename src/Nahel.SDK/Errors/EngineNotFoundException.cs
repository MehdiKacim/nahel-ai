namespace Nahel.SDK.Errors;
public class EngineNotFoundException : EngineException
{
    public EngineNotFoundException(string engineId) : base($"Engine '{engineId}' not found.", engineId) { }
}

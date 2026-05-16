namespace Nahel.SDK.Errors;
public class UnsupportedEngineCapabilityException : EngineException
{
    public UnsupportedEngineCapabilityException(string engineId, string capability) : base($"Engine '{engineId}' does not support '{capability}'.", engineId) { }
}

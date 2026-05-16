namespace Nahel.SDK.Errors;
public class ModelSwitchException : Exception
{
    public string ModelId { get; }
    public ModelSwitchException(string modelId, string message) : base(message) { ModelId = modelId; }
}

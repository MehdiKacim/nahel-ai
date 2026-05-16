namespace Nahel.SDK.Errors;
public class ModelLoadException : Exception
{
    public string ModelId { get; }
    public ModelLoadException(string modelId, string message) : base(message) { ModelId = modelId; }
}

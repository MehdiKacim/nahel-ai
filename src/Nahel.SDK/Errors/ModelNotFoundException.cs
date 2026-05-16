namespace Nahel.SDK.Errors;
public class ModelNotFoundException : Exception
{
    public string ModelId { get; }
    public ModelNotFoundException(string modelId) : base($"Model '{modelId}' not found.") { ModelId = modelId; }
}

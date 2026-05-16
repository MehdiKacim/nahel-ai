namespace Nahel.SDK.Models;
public sealed record EngineCapabilities
{
    public bool SupportsMultiModel { get; init; }
    public bool SupportsHotSwap { get; init; }
    public bool SupportsUnloadModel { get; init; }
    public bool SupportsConcurrentModels { get; init; }
    public bool SupportsOpenAiApi { get; init; }
    public bool SupportsOllamaApi { get; init; }
    public bool SupportsStreaming { get; init; }
    public bool SupportsEmbeddings { get; init; }
    public bool SupportsVision { get; init; }
    public bool SupportsUpdate { get; init; }
    public bool SupportsRuntimeUpdate { get; init; }
    public bool SupportsWarmup { get; init; }
    public bool SupportsMetrics { get; init; }
}

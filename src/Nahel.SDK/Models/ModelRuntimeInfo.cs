namespace Nahel.SDK.Models;
public sealed record ModelRuntimeInfo(string ModelId, ModelLoadState LoadState, DateTimeOffset? LoadedAt);

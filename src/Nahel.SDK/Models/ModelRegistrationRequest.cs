namespace Nahel.SDK.Models;
public sealed record ModelRegistrationRequest(string ModelId, string DisplayName, string EngineId, string? EngineModelName, string? ModelPath, ModelRuntimePolicy? Policy);

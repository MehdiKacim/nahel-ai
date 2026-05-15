namespace OllamaBridge.Core;

public interface IBackendLauncher
{
    Task<bool> EnsureRunningAsync(string backendId, string modelKey, CancellationToken ct = default);
    Task StopAsync(string backendId);
    Task<bool> IsHealthyAsync(string backendId, CancellationToken ct = default);
    string GetBackendUrl(string backendId);
}

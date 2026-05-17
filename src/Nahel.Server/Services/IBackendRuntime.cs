using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IBackendRuntime
{
    Task<JobResult> StartAsync(string engineId, CancellationToken ct = default);
    Task<JobResult> StopAsync(string engineId, CancellationToken ct = default);
    Task<JobResult> RestartAsync(string engineId, CancellationToken ct = default);
}

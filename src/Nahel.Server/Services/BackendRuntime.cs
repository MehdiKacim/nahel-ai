using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class BackendRuntime : IBackendRuntime
{
    private readonly IBackendCatalog _catalog;
    private readonly IBackendCommandQueue _queue;

    public BackendRuntime(IBackendCatalog catalog, IBackendCommandQueue queue)
    {
        _catalog = catalog;
        _queue = queue;
    }

    public Task<JobResult> StartAsync(string engineId, CancellationToken ct = default)
        => _queue.EnqueueAsync(new JobRequest(JobType.StartEngine, engineId, null, null), ct);

    public Task<JobResult> StopAsync(string engineId, CancellationToken ct = default)
        => _queue.EnqueueAsync(new JobRequest(JobType.StopEngine, engineId, null, null), ct);

    public Task<JobResult> RestartAsync(string engineId, CancellationToken ct = default)
        => _queue.EnqueueAsync(new JobRequest(JobType.RestartEngine, engineId, null, null), ct);
}

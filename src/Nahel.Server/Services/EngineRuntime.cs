using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class EngineRuntime : IEngineRuntime
{
    private readonly IEngineCatalog _catalog;
    private readonly IEngineCommandQueue _queue;

    public EngineRuntime(IEngineCatalog catalog, IEngineCommandQueue queue)
    {
        _catalog = catalog;
        _queue = queue;
    }

    public Task StartAsync(string engineId, CancellationToken ct = default)
        => _queue.EnqueueAsync(new JobRequest(JobType.StartEngine, engineId, null, null), ct);

    public Task StopAsync(string engineId, CancellationToken ct = default)
        => _queue.EnqueueAsync(new JobRequest(JobType.StopEngine, engineId, null, null), ct);

    public Task RestartAsync(string engineId, CancellationToken ct = default)
        => _queue.EnqueueAsync(new JobRequest(JobType.RestartEngine, engineId, null, null), ct);
}

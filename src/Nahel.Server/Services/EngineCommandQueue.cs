using System.Threading.Channels;
using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class EngineCommandQueue : BackgroundService, IEngineCommandQueue
{
    private readonly Channel<QueuedJob> _channel;
    private readonly List<JobInfo> _jobs = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EngineCommandQueue> _logger;
    private readonly object _lock = new();

    public EngineCommandQueue(IServiceProvider serviceProvider, ILogger<EngineCommandQueue> logger)
    {
        _channel = Channel.CreateUnbounded<QueuedJob>();
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<JobResult> EnqueueAsync(JobRequest request, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString("N")[..12];
        var job = new JobInfo(jobId, request.EngineId, request.Type, JobStatus.Queued, DateTimeOffset.UtcNow, null, null, null, Array.Empty<string>());
        lock (_lock) _jobs.Add(job);
        _channel.Writer.TryWrite(new QueuedJob(job, request));
        return Task.FromResult(new JobResult(jobId, true, "Queued"));
    }

    public Task<JobInfo?> GetJobAsync(string jobId, CancellationToken ct = default)
        => Task.FromResult(_jobs.FirstOrDefault(j => j.JobId == jobId));

    public IReadOnlyList<JobInfo> GetJobs(int count = 100)
        => _jobs.OrderByDescending(j => j.CreatedAt).Take(count).ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                UpdateJob(item.Job.JobId, j => j with { Status = JobStatus.Running, StartedAt = DateTimeOffset.UtcNow });
                await ExecuteJobAsync(item.Request, stoppingToken);
                UpdateJob(item.Job.JobId, j => j with { Status = JobStatus.Succeeded, CompletedAt = DateTimeOffset.UtcNow });
            }
            catch (Exception ex)
            {
                UpdateJob(item.Job.JobId, j => j with { Status = JobStatus.Failed, CompletedAt = DateTimeOffset.UtcNow, Error = ex.Message });
            }
        }
    }

    private async Task ExecuteJobAsync(JobRequest request, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IEngineCatalog>();
        var engine = catalog.GetEngine(request.EngineId ?? "");
        if (engine == null) throw new Exception($"Engine '{request.EngineId}' not found.");

        switch (request.Type)
        {
            case JobType.StartEngine:
                await engine.StartAsync(ct);
                break;
            case JobType.StopEngine:
                await engine.StopAsync(ct);
                break;
            case JobType.RestartEngine:
                await engine.RestartAsync(ct);
                break;
            case JobType.SwitchModel:
                if (request.ModelId != null)
                    await engine.SwitchModelAsync(new ModelSwitchRequest(request.ModelId, null), ct);
                break;
            default:
                throw new Exception($"Job type {request.Type} not supported.");
        }
    }

    private void UpdateJob(string jobId, Func<JobInfo, JobInfo> update)
    {
        lock (_lock)
        {
            var idx = _jobs.FindIndex(j => j.JobId == jobId);
            if (idx >= 0) _jobs[idx] = update(_jobs[idx]);
        }
    }

    private record QueuedJob(JobInfo Job, JobRequest Request);
}

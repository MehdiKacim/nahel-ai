using System.Diagnostics;
using System.Threading.Channels;
using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class BackendCommandQueue : BackgroundService, IBackendCommandQueue
{
    private readonly Channel<QueuedJob> _channel;
    private readonly List<JobInfo> _jobs = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackendCommandQueue> _logger;
    private readonly object _lock = new();

    public BackendCommandQueue(IServiceProvider serviceProvider, ILogger<BackendCommandQueue> logger)
    {
        _channel = Channel.CreateUnbounded<QueuedJob>();
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task<JobResult> EnqueueAsync(JobRequest request, CancellationToken ct = default)
    {
        try
        {
            var jobId = Guid.NewGuid().ToString("N")[..12];
            var job = new JobInfo(jobId, request.EngineId, request.Type, JobStatus.Queued, DateTimeOffset.UtcNow, null, null, null, Array.Empty<string>());
            lock (_lock) _jobs.Add(job);
            _channel.Writer.TryWrite(new QueuedJob(job, request));
            return Task.FromResult(new JobResult(jobId, true, "Queued"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnqueueAsync failed");
            throw;
        }
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
        var catalog = scope.ServiceProvider.GetRequiredService<IBackendCatalog>();
        var engine = catalog.GetBackend(request.EngineId ?? "");

        switch (request.Type)
        {
            case JobType.StartEngine:
                if (engine == null) throw new Exception($"Engine '{request.EngineId}' not found.");
                await engine.StartAsync(ct);
                break;
            case JobType.StopEngine:
                if (engine == null) throw new Exception($"Engine '{request.EngineId}' not found.");
                await engine.StopAsync(ct);
                break;
            case JobType.RestartEngine:
                if (engine == null) throw new Exception($"Engine '{request.EngineId}' not found.");
                await engine.RestartAsync(ct);
                break;
            case JobType.SwitchModel:
                if (engine == null) throw new Exception($"Engine '{request.EngineId}' not found.");
                if (request.ModelId != null)
                    await engine.SwitchModelAsync(new ModelSwitchRequest(request.ModelId, null), ct);
                break;
            case JobType.DownloadModel:
                await ExecuteDownloadAsync(request, ct);
                break;
            default:
                throw new Exception($"Job type {request.Type} not supported.");
        }
    }

    private async Task ExecuteDownloadAsync(JobRequest request, CancellationToken ct)
    {
        var repoId = request.Parameters?.GetValueOrDefault("repo_id");
        var localDir = request.Parameters?.GetValueOrDefault("local_dir");
        if (string.IsNullOrWhiteSpace(repoId)) throw new Exception("repo_id parameter is required.");
        if (string.IsNullOrWhiteSpace(localDir)) throw new Exception("local_dir parameter is required.");

        var targetDir = Path.GetFullPath(localDir);
        Directory.CreateDirectory(targetDir);

        var url = $"https://huggingface.co/{repoId}";
        _logger.LogInformation("[Download] Starting download of {RepoId} to {TargetDir} using git...", repoId, targetDir);

        // Try git lfs clone first, fallback to regular git clone
        var gitArgs = $"lfs clone --progress \"{url}\" \"{targetDir}\"";
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = gitArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = targetDir,
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<object?>();
        proc.Exited += (_, _) => tcs.TrySetResult(null);

        proc.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logger.LogDebug("[Download stdout] {Line}", e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                _logger.LogDebug("[Download stderr] {Line}", e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await using (ct.Register(() => { try { proc.Kill(true); } catch { } }))
        {
            await tcs.Task;
        }

        if (proc.ExitCode != 0)
        {
            // Fallback: try regular git clone without LFS
            _logger.LogWarning("git lfs clone failed (exit {ExitCode}), trying regular git clone...", proc.ExitCode);
            psi.Arguments = $"clone --progress \"{url}\" \"{targetDir}\"";
            using var proc2 = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var tcs2 = new TaskCompletionSource<object?>();
            proc2.Exited += (_, _) => tcs2.TrySetResult(null);
            proc2.Start();
            proc2.BeginOutputReadLine();
            proc2.BeginErrorReadLine();
            await using (ct.Register(() => { try { proc2.Kill(true); } catch { } }))
            {
                await tcs2.Task;
            }
            if (proc2.ExitCode != 0)
                throw new Exception($"git clone failed with exit code {proc2.ExitCode}.");
        }

        _logger.LogInformation("[Download] Completed: {RepoId} -> {TargetDir}", repoId, targetDir);
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

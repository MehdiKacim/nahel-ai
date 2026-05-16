using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IEngineCommandQueue
{
    Task<JobResult> EnqueueAsync(JobRequest request, CancellationToken ct = default);
    Task<JobInfo?> GetJobAsync(string jobId, CancellationToken ct = default);
    IReadOnlyList<JobInfo> GetJobs(int count = 100);
}

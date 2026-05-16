using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class InMemoryJobStore : IJobStore
{
    private readonly List<JobInfo> _jobs = new();

    public IReadOnlyList<JobInfo> GetRecent(int count = 100)
        => _jobs.OrderByDescending(j => j.CreatedAt).Take(count).ToList();

    public JobInfo? Get(string jobId)
        => _jobs.FirstOrDefault(j => j.JobId == jobId);

    public void Add(JobInfo job)
    {
        _jobs.Add(job);
    }
}

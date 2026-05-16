using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IJobStore
{
    IReadOnlyList<JobInfo> GetRecent(int count = 100);
    JobInfo? Get(string jobId);
    void Add(JobInfo job);
}

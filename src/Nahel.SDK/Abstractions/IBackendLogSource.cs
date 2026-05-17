using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IBackendLogSource
{
    IAsyncEnumerable<LogEvent> ReadLogsAsync(CancellationToken ct = default);
}

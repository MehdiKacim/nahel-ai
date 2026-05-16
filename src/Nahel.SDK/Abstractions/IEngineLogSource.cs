using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IEngineLogSource
{
    IAsyncEnumerable<LogEvent> ReadLogsAsync(CancellationToken ct = default);
}

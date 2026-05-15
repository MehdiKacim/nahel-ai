using Microsoft.Extensions.Hosting;

namespace Ollamock.Service.Bridge;

public class BridgeLifecycleService : BackgroundService
{
    private readonly LogSink _logSink;

    public BridgeLifecycleService(LogSink logSink) => _logSink = logSink;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logSink.Write("INF", "Ollamock.Service demarre");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logSink.Write("INF", "Ollamock.Service s'arrete");
        return base.StopAsync(cancellationToken);
    }
}

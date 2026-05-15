using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OllamaBridge.Services;

public class BridgeLifecycleService : BackgroundService
{
    private readonly ILogger<BridgeLifecycleService> _logger;
    private readonly LogSink _logSink;

    public BridgeLifecycleService(ILogger<BridgeLifecycleService> logger, LogSink logSink)
    {
        _logger = logger;
        _logSink = logSink;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logSink.Write("INF", "OllamaBridge demarre");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logSink.Write("INF", "OllamaBridge s'arrete");
        return base.StopAsync(cancellationToken);
    }
}

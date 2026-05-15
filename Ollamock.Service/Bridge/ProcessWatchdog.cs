using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ollamock.Service.Bridge;

public class ProcessWatchdog : BackgroundService
{
    private readonly ILogger<ProcessWatchdog> _logger;
    private readonly BridgeConfig _config;
    private readonly IBackendLauncher _launcher;
    private readonly LogSink _logSink;

    public ProcessWatchdog(
        ILogger<ProcessWatchdog> logger,
        IOptions<BridgeConfig> config,
        IBackendLauncher launcher,
        LogSink logSink)
    {
        _logger = logger;
        _config = config.Value;
        _launcher = launcher;
        _logSink = logSink;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Watchdog logic - check all managed providers
            await Task.Delay(TimeSpan.FromSeconds(_config.HealthCheckIntervalSeconds), stoppingToken);
        }
    }
}

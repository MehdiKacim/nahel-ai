using OllamaBridge.Config;
using OllamaBridge.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OllamaBridge.Services;

public class ProcessWatchdog : BackgroundService
{
    private readonly ILogger<ProcessWatchdog> _logger;
    private readonly AppSettings _config;
    private readonly IBackendLauncher _launcher;
    private readonly LogSink _logSink;

    public ProcessWatchdog(
        ILogger<ProcessWatchdog> logger,
        IOptions<AppSettings> config,
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
            foreach (var (backendId, backend) in _config.Backends)
            {
                if (!backend.AutoStart) continue;

                var healthy = await _launcher.IsHealthyAsync(backendId, stoppingToken);
                if (!healthy)
                {
                    _logSink.Write("WRN", $"[watchdog] {backendId} unhealthy, checking...");
                    // Le launcher gere lui-meme le compteur de restart
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_config.Bridge.HealthCheckIntervalSeconds), stoppingToken);
        }
    }
}

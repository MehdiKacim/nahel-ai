namespace Nahel.Server.Services;

public interface IEngineRuntime
{
    Task StartAsync(string engineId, CancellationToken ct = default);
    Task StopAsync(string engineId, CancellationToken ct = default);
    Task RestartAsync(string engineId, CancellationToken ct = default);
}

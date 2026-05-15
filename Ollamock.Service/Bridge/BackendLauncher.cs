using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;

namespace Ollamock.Service.Bridge;

public class BackendLauncher : IBackendLauncher, IDisposable
{
    private readonly ILogger<BackendLauncher> _logger;
    private readonly BridgeConfig _bridgeConfig;
    private readonly ProvidersConfig _providers;
    private readonly ModelsConfig _models;
    private readonly LogSink _logSink;
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly ConcurrentDictionary<string, int> _restartCount = new();
    private readonly ConcurrentDictionary<string, bool> _ready = new();
    private readonly ConcurrentDictionary<string, DateTime> _startTime = new();

    public BackendLauncher(
        ILogger<BackendLauncher> logger,
        IOptions<BridgeConfig> bridgeConfig,
        IOptions<ProvidersConfig> providers,
        IOptions<ModelsConfig> models,
        LogSink logSink)
    {
        _logger = logger;
        _bridgeConfig = bridgeConfig.Value;
        _providers = providers.Value;
        _models = models.Value;
        _logSink = logSink;
    }

    public async Task<bool> EnsureRunningAsync(string providerId, string modelKey, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerId, out var provider))
            throw new InvalidOperationException($"Provider '{providerId}' non defini");

        if (!_models.TryGetValue(modelKey, out var model))
            throw new InvalidOperationException($"Modele '{modelKey}' non defini");

        if (_ready.TryGetValue(providerId, out var isReady) && isReady)
            return true;

        if (!provider.AutoStart || model.AutoStart == false)
        {
            if (await IsPortOpenAsync(provider.Port, ct))
            {
                _ready[providerId] = true;
                return true;
            }
            return false;
        }

        if (_restartCount.TryGetValue(providerId, out var count) && count >= _bridgeConfig.MaxBackendRestarts)
        {
            _logSink.Write("ERR", $"[{providerId}] Max restarts atteint ({_bridgeConfig.MaxBackendRestarts})");
            return false;
        }

        var args = provider.Arguments
            .Replace("{modelPath}", $"\"{model.ModelPath}\"")
            .Replace("{mmprojPath}", $"\"{model.MmprojPath ?? ""}\"")
            .Replace("{port}", provider.Port.ToString())
            .Replace("{contextSize}", (model.ContextSize ?? 4096).ToString())
            .Replace("{device}", model.Device ?? "AUTO")
            .Replace("{fallbackDevice}", model.FallbackDevice ?? "CPU");

        var psi = new ProcessStartInfo
        {
            FileName = provider.Executable,
            Arguments = args,
            WorkingDirectory = provider.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var (key, value) in provider.EnvironmentVariables)
            psi.EnvironmentVariables[key] = value;

        _logSink.Write("INF", $"[{providerId}] Demarrage: {provider.Executable} {args}");

        var proc = Process.Start(psi);
        if (proc == null)
        {
            _restartCount.AddOrUpdate(providerId, 1, (_, v) => v + 1);
            return false;
        }

        _processes[providerId] = proc;
        _startTime[providerId] = DateTime.UtcNow;

        _ = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardOutput.ReadLineAsync(ct);
                if (line != null) _logSink.Write("DBG", $"[{providerId}] {line}");
            }
        }, ct);

        _ = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardError.ReadLineAsync(ct);
                if (line != null) _logSink.Write("ERR", $"[{providerId}] {line}");
            }
        }, ct);

        for (int i = 0; i < 60; i++)
        {
            if (await IsHealthyAsync(providerId, ct))
            {
                _ready[providerId] = true;
                _restartCount[providerId] = 0;
                _logSink.Write("INF", $"[{providerId}] Pret sur port {provider.Port}");
                return true;
            }
            await Task.Delay(1000, ct);
        }

        _restartCount.AddOrUpdate(providerId, 1, (_, v) => v + 1);
        _logSink.Write("WRN", $"[{providerId}] Health check echoue apres 60s");
        return false;
    }

    public async Task<bool> IsHealthyAsync(string providerId, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerId, out var provider))
            return false;

        if (!await IsPortOpenAsync(provider.Port, ct))
            return false;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"http://localhost:{provider.Port}{provider.HealthEndpoint}", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public Task StopAsync(string providerId)
    {
        if (_processes.TryRemove(providerId, out var proc) && !proc.HasExited)
        {
            try
            {
                _logSink.Write("INF", $"[{providerId}] Arret (PID {proc.Id})");
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logSink.Write("ERR", $"[{providerId}] Echec arret: {ex.Message}");
            }
        }
        _ready.TryRemove(providerId, out _);
        _startTime.TryRemove(providerId, out _);
        return Task.CompletedTask;
    }

    public string GetProviderUrl(string providerId)
    {
        if (!_providers.TryGetValue(providerId, out var provider))
            throw new InvalidOperationException($"Provider inconnu: {providerId}");
        return $"http://localhost:{provider.Port}";
    }

    public Process? GetProcess(string providerId) =>
        _processes.TryGetValue(providerId, out var proc) && !proc.HasExited ? proc : null;

    public DateTime? GetStartTime(string providerId) =>
        _startTime.TryGetValue(providerId, out var dt) ? dt : null;

    public int GetRestartCount(string providerId) =>
        _restartCount.TryGetValue(providerId, out var c) ? c : 0;

    private static async Task<bool> IsPortOpenAsync(int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port);
            return true;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        foreach (var (name, proc) in _processes)
        {
            try
            {
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch { }
        }
        _processes.Clear();
    }
}

using OllamaBridge.Config;
using OllamaBridge.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;

namespace OllamaBridge.Services;

public class BackendLauncher : IBackendLauncher, IDisposable
{
    private readonly ILogger<BackendLauncher> _logger;
    private readonly AppSettings _config;
    private readonly LogSink _logSink;
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly ConcurrentDictionary<string, int> _restartCount = new();
    private readonly ConcurrentDictionary<string, bool> _ready = new();
    private readonly ConcurrentDictionary<string, DateTime> _startTime = new();

    public BackendLauncher(ILogger<BackendLauncher> logger, IOptions<AppSettings> config, LogSink logSink)
    {
        _logger = logger;
        _config = config.Value;
        _logSink = logSink;
    }

    public async Task<bool> EnsureRunningAsync(string backendId, string modelKey, CancellationToken ct = default)
    {
        if (!_config.Backends.TryGetValue(backendId, out var backend))
            throw new InvalidOperationException($"Backend '{backendId}' non defini");

        if (!_config.Models.TryGetValue(modelKey, out var model))
            throw new InvalidOperationException($"Modele '{modelKey}' non defini");

        if (_ready.TryGetValue(backendId, out var isReady) && isReady)
            return true;

        if (!backend.AutoStart || model.AutoStart == false)
        {
            if (await IsPortOpenAsync(backend.Port, ct))
            {
                _ready[backendId] = true;
                return true;
            }
            return false;
        }

        if (_restartCount.TryGetValue(backendId, out var count) && count >= _config.Bridge.MaxBackendRestarts)
        {
            _logSink.Write("ERR", $"Backend {backendId} a depasse {_config.Bridge.MaxBackendRestarts} redemarrages");
            return false;
        }

        var args = backend.Arguments
            .Replace("{modelPath}", $"\"{model.ModelPath}\"")
            .Replace("{mmprojPath}", $"\"{model.MmprojPath ?? ""}\"")
            .Replace("{port}", backend.Port.ToString())
            .Replace("{contextSize}", (model.ContextSize ?? 4096).ToString())
            .Replace("{device}", model.Device ?? "AUTO")
            .Replace("{fallbackDevice}", model.FallbackDevice ?? "CPU");

        var psi = new ProcessStartInfo
        {
            FileName = backend.Executable,
            Arguments = args,
            WorkingDirectory = backend.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var (key, value) in backend.EnvironmentVariables)
            psi.EnvironmentVariables[key] = value;

        _logSink.Write("INF", $"[{backendId}] Demarrage: {backend.Executable} {args}");

        var proc = Process.Start(psi);
        if (proc == null)
        {
            _restartCount.AddOrUpdate(backendId, 1, (_, v) => v + 1);
            return false;
        }

        _processes[backendId] = proc;
        _startTime[backendId] = DateTime.UtcNow;

        _ = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardOutput.ReadLineAsync(ct);
                if (line != null) _logSink.Write("DBG", $"[{backendId}] {line}");
            }
        }, ct);

        _ = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardError.ReadLineAsync(ct);
                if (line != null) _logSink.Write("ERR", $"[{backendId}] {line}");
            }
        }, ct);

        for (int i = 0; i < 60; i++)
        {
            if (await IsHealthyAsync(backendId, ct))
            {
                _ready[backendId] = true;
                _restartCount[backendId] = 0;
                _logSink.Write("INF", $"[{backendId}] Pret sur port {backend.Port}");
                return true;
            }
            await Task.Delay(1000, ct);
        }

        _restartCount.AddOrUpdate(backendId, 1, (_, v) => v + 1);
        _logSink.Write("WRN", $"[{backendId}] Health check echoue apres 60s");
        return false;
    }

    public async Task<bool> IsHealthyAsync(string backendId, CancellationToken ct = default)
    {
        if (!_config.Backends.TryGetValue(backendId, out var backend))
            return false;

        if (!await IsPortOpenAsync(backend.Port, ct))
            return false;

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"http://localhost:{backend.Port}{backend.HealthEndpoint}", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task StopAsync(string backendId)
    {
        if (_processes.TryRemove(backendId, out var proc) && !proc.HasExited)
        {
            try
            {
                _logSink.Write("INF", $"[{backendId}] Arret (PID {proc.Id})");
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logSink.Write("ERR", $"[{backendId}] Echec arret: {ex.Message}");
            }
        }
        _ready.TryRemove(backendId, out _);
        _startTime.TryRemove(backendId, out _);
        return Task.CompletedTask;
    }

    public string GetBackendUrl(string backendId)
    {
        if (!_config.Backends.TryGetValue(backendId, out var backend))
            throw new InvalidOperationException($"Backend inconnu: {backendId}");
        return $"http://localhost:{backend.Port}";
    }

    public Process? GetProcess(string backendId) =>
        _processes.TryGetValue(backendId, out var proc) && !proc.HasExited ? proc : null;

    public DateTime? GetStartTime(string backendId) =>
        _startTime.TryGetValue(backendId, out var dt) ? dt : null;

    public int GetRestartCount(string backendId) =>
        _restartCount.TryGetValue(backendId, out var c) ? c : 0;

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

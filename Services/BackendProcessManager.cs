using OllamaBridge.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;

namespace OllamaBridge.Services;

public class BackendProcessManager : IDisposable
{
    private readonly ILogger<BackendProcessManager> _logger;
    private readonly AppSettings _config;
    private readonly ConcurrentDictionary<string, Process> _processes = new();
    private readonly ConcurrentDictionary<string, bool> _ready = new();

    public BackendProcessManager(ILogger<BackendProcessManager> logger, IOptions<AppSettings> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task EnsureBackendAsync(string backendName, string modelKey, CancellationToken ct = default)
    {
        if (!_config.Backends.TryGetValue(backendName, out var backend))
            throw new InvalidOperationException($"Backend '{backendName}' non defini");

        if (!_config.Models.TryGetValue(modelKey, out var model))
            throw new InvalidOperationException($"Modele '{modelKey}' non defini");

        if (_ready.TryGetValue(backendName, out var isReady) && isReady)
            return;

        if (!backend.AutoStart || model.AutoStart == false)
        {
            if (await IsPortOpenAsync(backend.Port, ct))
            {
                _ready[backendName] = true;
                return;
            }
            throw new InvalidOperationException($"Backend {backendName} non demarre (AutoStart desactive)");
        }

        var args = backend.Arguments
            .Replace("{modelPath}", $"\"{model.ModelPath}\"")
            .Replace("{mmprojPath}", $"\"{model.MmprojPath ?? ""}\"")
            .Replace("{port}", backend.Port.ToString())
            .Replace("{contextSize}", (model.ContextSize ?? 4096).ToString());

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

        _logger.LogInformation("Demarrage {Backend} : {Exe} {Args}", backendName, backend.Executable, args);

        var proc = Process.Start(psi) ?? throw new InvalidOperationException("Echec demarrage process");
        _processes[backendName] = proc;

        _ = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardOutput.ReadLineAsync(ct);
                if (line != null) _logger.LogDebug("[{Backend}] {Line}", backendName, line);
            }
        }, ct);

        _ = Task.Run(async () =>
        {
            while (!proc.HasExited)
            {
                var line = await proc.StandardError.ReadLineAsync(ct);
                if (line != null) _logger.LogError("[{Backend}] {Line}", backendName, line);
            }
        }, ct);

        for (int i = 0; i < 60; i++)
        {
            if (await IsPortOpenAsync(backend.Port, ct))
            {
                _ready[backendName] = true;
                _logger.LogInformation("{Backend} pret sur port {Port}", backendName, backend.Port);
                return;
            }
            await Task.Delay(1000, ct);
        }

        throw new TimeoutException($"Backend {backendName} non repondeur apres 60s");
    }

    public string GetBackendUrl(string backendName)
    {
        if (!_config.Backends.TryGetValue(backendName, out var backend))
            throw new InvalidOperationException($"Backend inconnu: {backendName}");
        return $"http://localhost:{backend.Port}";
    }

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
                _logger.LogInformation("Arret {Backend} (PID {Pid})", name, proc.Id);
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Echec arret {Backend}", name);
            }
        }
        _processes.Clear();
    }
}

using System.Diagnostics;

namespace Nahel.Engine.Ovms;

public sealed class OvmsProcessSupervisor : IDisposable
{
    private Process? _process;
    private readonly object _lock = new();

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _process != null && !_process.HasExited;
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_lock)
            {
                return _process?.Id;
            }
        }
    }

    public event EventHandler<string>? LogReceived;

    public async Task<bool> StartAsync(OvmsOptions options, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_process != null && !_process.HasExited)
                return true;
        }

        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
            return false;

        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = options.WorkingDirectory
        };

        if (!string.IsNullOrEmpty(options.ConfigPath))
        {
            startInfo.ArgumentList.Add("--config_path");
            startInfo.ArgumentList.Add(options.ConfigPath);
        }

        if (options.RestPort > 0)
        {
            startInfo.ArgumentList.Add("--rest_port");
            startInfo.ArgumentList.Add(options.RestPort.ToString());
        }

        if (options.GrpcPort > 0)
        {
            startInfo.ArgumentList.Add("--grpc_port");
            startInfo.ArgumentList.Add(options.GrpcPort.ToString());
        }

        foreach (var env in options.EnvironmentVariables)
        {
            startInfo.EnvironmentVariables[env.Key] = env.Value;
        }

        var process = new Process { StartInfo = startInfo };
        bool started = process.Start();
        if (!started)
            return false;

        lock (_lock)
        {
            _process = process;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await process.StandardOutput.ReadLineAsync(ct)) != null)
                {
                    LogReceived?.Invoke(this, line);
                }
            }
            catch
            {
                // ignore
            }
        }, ct);

        _ = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await process.StandardError.ReadLineAsync(ct)) != null)
                {
                    LogReceived?.Invoke(this, line);
                }
            }
            catch
            {
                // ignore
            }
        }, ct);

        await Task.Yield();
        return true;
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Process? process;
        lock (_lock)
        {
            process = _process;
            _process = null;
        }

        if (process == null || process.HasExited)
            return;

        try
        {
            process.CloseMainWindow();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // timeout or cancellation
            }
        }
        catch
        {
            // ignore
        }

        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // timeout or cancellation
                }
            }
            catch
            {
                // ignore
            }
        }

        process.Dispose();
    }

    public async Task<bool> RestartAsync(OvmsOptions options, CancellationToken ct = default)
    {
        await StopAsync(ct);
        return await StartAsync(options, ct);
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }
}

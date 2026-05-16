using System.Diagnostics;
using Nahel.SDK.Models;

namespace Nahel.Ollamock.System.Processes;

public sealed class ToolProcessSupervisor
{
    public string ToolId { get; }
    public bool IsRunning => _process != null && !_process.HasExited;
    public int? ProcessId => _process?.Id;
    public DateTimeOffset? StartedAt { get; private set; }

    public string? Executable { get; set; }
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }

    private Process? _process;
    private readonly List<LogEvent> _logs = new();
    private readonly object _lock = new();

    public ToolProcessSupervisor(string toolId)
    {
        ToolId = toolId;
    }

    public Task<ToolLaunchResult> StartAsync(ToolLaunchRequest request, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (IsRunning)
            {
                return Task.FromResult(new ToolLaunchResult(true, "Already running", ProcessId));
            }

            if (string.IsNullOrWhiteSpace(Executable))
            {
                throw new InvalidOperationException("Executable must be set before starting.");
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = Executable,
            Arguments = Arguments ?? string.Empty,
            WorkingDirectory = WorkingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (request.EnvironmentVariables != null)
        {
            foreach (var kvp in request.EnvironmentVariables)
            {
                psi.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        _process = new Process { StartInfo = psi };
        _process.Start();
        StartedAt = DateTimeOffset.UtcNow;
        var pid = _process.Id;

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested && _process != null)
            {
                string? line;
                try
                {
                    line = await _process.StandardOutput.ReadLineAsync(ct);
                }
                catch
                {
                    break;
                }

                if (line == null) break;

                lock (_lock)
                {
                    _logs.Add(new LogEvent("Info", line, DateTimeOffset.UtcNow, "stdout"));
                }
            }
        }, ct);

        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested && _process != null)
            {
                string? line;
                try
                {
                    line = await _process.StandardError.ReadLineAsync(ct);
                }
                catch
                {
                    break;
                }

                if (line == null) break;

                lock (_lock)
                {
                    _logs.Add(new LogEvent("Error", line, DateTimeOffset.UtcNow, "stderr"));
                }
            }
        }, ct);

        return Task.FromResult(new ToolLaunchResult(true, "Started", pid));
    }

    public async Task<ToolStopResult> StopAsync(ToolStopRequest request, CancellationToken ct = default)
    {
        Process? process;
        lock (_lock)
        {
            process = _process;
        }

        if (process == null || process.HasExited)
        {
            return new ToolStopResult(true, "Not running");
        }

        bool closed = false;
        if (!request.Force)
        {
            try
            {
                closed = process.CloseMainWindow();
            }
            catch
            {
                closed = false;
            }
        }

        if (!closed)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort
            }
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort
            }
        }

        return new ToolStopResult(true, "Stopped");
    }

    public ToolStatus GetStatus()
    {
        return new ToolStatus(ToolId, IsRunning, ProcessId);
    }

    public IReadOnlyList<LogEvent> GetLogs(int count = 100)
    {
        lock (_lock)
        {
            return _logs.TakeLast(count).ToList();
        }
    }
}

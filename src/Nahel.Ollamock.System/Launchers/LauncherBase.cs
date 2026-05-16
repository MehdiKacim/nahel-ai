using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nahel.Ollamock.System.Backup;
using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Ollamock.System.Launchers;

public abstract class LauncherBase : IToolLauncher
{
    public abstract string ToolId { get; }
    public abstract string DisplayName { get; }

    protected readonly IConfigBackup _backup;
    protected readonly ILogger _logger;

    protected LauncherBase(IConfigBackup backup, ILogger logger)
    {
        _backup = backup;
        _logger = logger;
    }

    public abstract Task<ToolStatus> GetStatusAsync(CancellationToken ct = default);
    public abstract Task<ToolLaunchResult> StartAsync(ToolLaunchRequest request, CancellationToken ct = default);
    public abstract Task<ToolStopResult> StopAsync(ToolStopRequest request, CancellationToken ct = default);

    protected static async Task<bool> CommandExistsAsync(string command)
    {
        foreach (var finder in new[] { "where", "which" })
        {
            var psi = new ProcessStartInfo
            {
                FileName = finder,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var proc = Process.Start(psi);
                if (proc == null) continue;
                await proc.WaitForExitAsync();
                if (proc.ExitCode == 0) return true;
            }
            catch
            {
                continue;
            }
        }

        return false;
    }

    protected static async Task<string?> GetCommandVersionAsync(string command, string versionArg = "--version")
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = versionArg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0 ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    protected static void SetEnvVar(string key, string value, EnvironmentVariableTarget target = EnvironmentVariableTarget.User)
    {
        Environment.SetEnvironmentVariable(key, value, target);
    }

    protected static string? GetEnvVar(string key, EnvironmentVariableTarget target = EnvironmentVariableTarget.User)
    {
        return Environment.GetEnvironmentVariable(key, target);
    }

    protected static async Task<ToolLaunchResult> RunCommandAsync(string fileName, string arguments, int timeoutMs = 120000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return new ToolLaunchResult(false, "Failed to start process", null);

        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return new ToolLaunchResult(false, "Command timed out", proc.Id);
        }

        var output = await proc.StandardOutput.ReadToEndAsync();
        return new ToolLaunchResult(proc.ExitCode == 0, output.Trim(), proc.Id);
    }

    protected static async Task<string?> GetCommandPathAsync(string command)
    {
        foreach (var finder in new[] { "where", "which" })
        {
            var psi = new ProcessStartInfo
            {
                FileName = finder,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var proc = Process.Start(psi);
                if (proc == null) continue;
                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                if (proc.ExitCode == 0)
                {
                    var path = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    return path?.Trim();
                }
            }
            catch
            {
                continue;
            }
        }

        return null;
    }

    protected static async Task<bool> IsProcessRunningAsync(string processName)
    {
        return await Task.Run(() => Process.GetProcessesByName(processName).Length > 0);
    }

    protected static async Task KillProcessAsync(string processName)
    {
        await Task.Run(() =>
        {
            foreach (var proc in Process.GetProcessesByName(processName))
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
            }
        });
    }
}

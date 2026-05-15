using System.Diagnostics;
using System.Text.Json;

namespace Ollamock.Service.Launchers;

public interface ILauncher
{
    string Name { get; }
    string DisplayName { get; }
    string Description { get; }
    string ApiFormat { get; } // "openai", "anthropic", "generic"
    string Category { get; } // "cli", "desktop", "vscode", "web"

    // Detection
    Task<DetectResult> DetectAsync();

    // Installation
    Task<InstallResult> InstallAsync();
    Task<bool> IsInstallSupportedAsync();

    // Configuration
    Task ConfigureAsync(string bridgeUrl, string? model = null);
    Task RestoreAsync();
    Task<bool> IsConfiguredAsync();

    // Lifecycle
    Task LaunchAsync(string? model = null);
    Task<bool> IsRunningAsync();
    Task StopAsync();

    // Metadata
    string? GetInstallCommand();
    string? GetHomepageUrl();
    string? GetDocumentationUrl();
}

public record DetectResult(
    bool Installed, 
    string? Version = null, 
    string? Path = null, 
    string? InstallHint = null);

public record InstallResult(
    bool Success, 
    string? Error = null, 
    string? Output = null);

public abstract class LauncherBase : ILauncher
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract string ApiFormat { get; }
    public abstract string Category { get; }

    protected readonly IConfigBackup _backup;
    protected readonly ILogger _logger;

    protected LauncherBase(IConfigBackup backup, ILogger logger)
    {
        _backup = backup;
        _logger = logger;
    }

    public virtual Task<DetectResult> DetectAsync() => Task.FromResult(new DetectResult(false));
    public virtual Task<InstallResult> InstallAsync() => Task.FromResult(new InstallResult(false, "Auto-install not supported"));
    public virtual Task<bool> IsInstallSupportedAsync() => Task.FromResult(false);

    public abstract Task ConfigureAsync(string bridgeUrl, string? model = null);
    public virtual Task RestoreAsync() => Task.CompletedTask;
    public virtual Task<bool> IsConfiguredAsync() => Task.FromResult(false);

    public virtual Task LaunchAsync(string? model = null) => Task.CompletedTask;
    public virtual Task<bool> IsRunningAsync() => Task.FromResult(false);
    public virtual Task StopAsync() => Task.CompletedTask;

    public virtual string? GetInstallCommand() => null;
    public virtual string? GetHomepageUrl() => null;
    public virtual string? GetDocumentationUrl() => null;

    // Helpers
    protected static async Task<bool> CommandExistsAsync(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0;
        }
        catch { return false; }
    }

    protected static async Task<string?> GetCommandVersionAsync(string command, string versionArg = "--version")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = versionArg,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Trim();
        }
        catch { return null; }
    }

    protected static void SetEnvVar(string key, string value, EnvironmentVariableTarget target = EnvironmentVariableTarget.User)
    {
        Environment.SetEnvironmentVariable(key, value, target);
    }

    protected static string? GetEnvVar(string key, EnvironmentVariableTarget target = EnvironmentVariableTarget.User)
    {
        return Environment.GetEnvironmentVariable(key, target);
    }

    protected static async Task<InstallResult> RunCommandAsync(string fileName, string arguments, int timeoutMs = 120000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return new InstallResult(false, "Failed to start process");

            var cts = new CancellationTokenSource(timeoutMs);
            await proc.WaitForExitAsync(cts.Token);

            var output = await proc.StandardOutput.ReadToEndAsync();
            var error = await proc.StandardError.ReadToEndAsync();

            return proc.ExitCode == 0 
                ? new InstallResult(true, Output: output) 
                : new InstallResult(false, Error: error);
        }
        catch (Exception ex)
        {
            return new InstallResult(false, Error: ex.Message);
        }
    }
}

using Microsoft.Extensions.Logging;
using Nahel.SDK.Models;
using Nahel.Ollamock.System.Backup;
using Nahel.Ollamock.System.Processes;

namespace Nahel.Ollamock.System.Launchers;

public sealed class ClaudeCodeLauncher : LauncherBase
{
    public override string ToolId => "claude";
    public override string DisplayName => "Claude Code";

    private readonly ToolProcessSupervisor _supervisor;

    public ClaudeCodeLauncher(IConfigBackup backup, ILogger<ClaudeCodeLauncher> logger) : base(backup, logger)
    {
        _supervisor = new ToolProcessSupervisor(ToolId);
    }

    public override Task<ToolStatus> GetStatusAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_supervisor.GetStatus());
    }

    public override async Task<ToolLaunchResult> StartAsync(ToolLaunchRequest request, CancellationToken ct = default)
    {
        var baseUrl = (request.EnvironmentVariables?.TryGetValue("ANTHROPIC_BASE_URL", out var bu) == true ? bu : null)
            ?? "http://localhost:11434";

        var envVars = new Dictionary<string, string>(request.EnvironmentVariables ?? new Dictionary<string, string>());
        envVars["ANTHROPIC_BASE_URL"] = baseUrl;
        envVars["ANTHROPIC_AUTH_TOKEN"] = "local";
        envVars["ANTHROPIC_API_KEY"] = "";
        envVars["ANTHROPIC_VERSION"] = "2023-06-01";

        var oldVars = new Dictionary<string, string>();
        foreach (var key in new[] { "ANTHROPIC_BASE_URL", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_API_KEY", "ANTHROPIC_VERSION" })
        {
            var val = GetEnvVar(key);
            if (!string.IsNullOrEmpty(val))
            {
                oldVars[key] = val;
            }
        }

        if (oldVars.Count > 0)
        {
            await _backup.BackupAsync(ToolId, oldVars);
        }

        foreach (var kvp in envVars)
        {
            SetEnvVar(kvp.Key, kvp.Value);
        }

        _supervisor.Executable = await GetCommandPathAsync("claude") ?? "claude";
        _supervisor.Arguments = string.Empty;

        var newRequest = request with { EnvironmentVariables = envVars };
        return await _supervisor.StartAsync(newRequest, ct);
    }

    public override async Task<ToolStopResult> StopAsync(ToolStopRequest request, CancellationToken ct = default)
    {
        await KillProcessAsync("claude");
        return await _supervisor.StopAsync(request, ct);
    }
}

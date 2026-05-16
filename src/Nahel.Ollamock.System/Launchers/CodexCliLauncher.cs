using Microsoft.Extensions.Logging;
using Nahel.SDK.Models;
using Nahel.Ollamock.System.Backup;
using Nahel.Ollamock.System.Processes;

namespace Nahel.Ollamock.System.Launchers;

public sealed class CodexCliLauncher : LauncherBase
{
    public override string ToolId => "codex";
    public override string DisplayName => "Codex CLI";

    private readonly ToolProcessSupervisor _supervisor;

    public CodexCliLauncher(IConfigBackup backup, ILogger<CodexCliLauncher> logger) : base(backup, logger)
    {
        _supervisor = new ToolProcessSupervisor(ToolId);
    }

    public override Task<ToolStatus> GetStatusAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_supervisor.GetStatus());
    }

    public override async Task<ToolLaunchResult> StartAsync(ToolLaunchRequest request, CancellationToken ct = default)
    {
        var baseUrl = (request.EnvironmentVariables?.TryGetValue("OPENAI_BASE_URL", out var bu) == true ? bu : null)
            ?? "http://localhost:11434";

        if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl += "/v1";
        }

        var envVars = new Dictionary<string, string>(request.EnvironmentVariables ?? new Dictionary<string, string>());
        envVars["OPENAI_BASE_URL"] = baseUrl;
        envVars["OPENAI_API_KEY"] = "local";

        var oldVars = new Dictionary<string, string>();
        foreach (var key in new[] { "OPENAI_BASE_URL", "OPENAI_API_KEY" })
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

        SetEnvVar("OPENAI_BASE_URL", baseUrl);
        SetEnvVar("OPENAI_API_KEY", "local");

        _supervisor.Executable = await GetCommandPathAsync("codex") ?? "codex";
        _supervisor.Arguments = string.IsNullOrEmpty(request.Model) ? "--oss" : $"-m {request.Model}";

        var newRequest = request with { EnvironmentVariables = envVars };
        return await _supervisor.StartAsync(newRequest, ct);
    }

    public override async Task<ToolStopResult> StopAsync(ToolStopRequest request, CancellationToken ct = default)
    {
        await KillProcessAsync("codex");
        return await _supervisor.StopAsync(request, ct);
    }
}

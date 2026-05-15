namespace Ollamock.Service.Launchers;

public class ClaudeCodeLauncher : LauncherBase
{
    public override string Name => "claude";
    public override string DisplayName => "Claude Code";
    public override string Description => "Anthropic Claude Code CLI - AI coding assistant";
    public override string ApiFormat => "anthropic";
    public override string Category => "cli";

    private const string ENV_BASE_URL = "ANTHROPIC_BASE_URL";
    private const string ENV_AUTH_TOKEN = "ANTHROPIC_AUTH_TOKEN";
    private const string ENV_API_KEY = "ANTHROPIC_API_KEY";
    private const string ENV_VERSION = "ANTHROPIC_VERSION";
    private const string ENV_ATTRIBUTION = "CLAUDE_CODE_ATTRIBUTION_HEADER";

    public ClaudeCodeLauncher(IConfigBackup backup, ILogger<ClaudeCodeLauncher> logger) : base(backup, logger) { }

    public override async Task<DetectResult> DetectAsync()
    {
        var exists = await CommandExistsAsync("claude");
        if (!exists) return new DetectResult(false, InstallHint: "npm install -g @anthropic-ai/claude-code");

        var version = await GetCommandVersionAsync("claude", "--version");
        return new DetectResult(true, Version: version, Path: await GetCommandPathAsync("claude"));
    }

    public override Task<bool> IsInstallSupportedAsync() => Task.FromResult(true);

    public override async Task<InstallResult> InstallAsync()
    {
        return await RunCommandAsync("npm", "install -g @anthropic-ai/claude-code", timeoutMs: 300000);
    }

    public override string? GetInstallCommand() => "npm install -g @anthropic-ai/claude-code";
    public override string? GetHomepageUrl() => "https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview";
    public override string? GetDocumentationUrl() => "https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/tutorials";

    public override async Task ConfigureAsync(string bridgeUrl, string? model = null)
    {
        // Backup current config
        var envVars = new Dictionary<string, string>
        {
            [ENV_BASE_URL] = GetEnvVar(ENV_BASE_URL) ?? "",
            [ENV_AUTH_TOKEN] = GetEnvVar(ENV_AUTH_TOKEN) ?? "",
            [ENV_API_KEY] = GetEnvVar(ENV_API_KEY) ?? "",
            [ENV_VERSION] = GetEnvVar(ENV_VERSION) ?? "",
            [ENV_ATTRIBUTION] = GetEnvVar(ENV_ATTRIBUTION) ?? ""
        };
        await _backup.BackupAsync(Name, envVars);

        // Set new config - IMPORTANT: no /v1 for Anthropic
        SetEnvVar(ENV_BASE_URL, bridgeUrl.TrimEnd('/')); // http://localhost:11434
        SetEnvVar(ENV_AUTH_TOKEN, "ollama");
        SetEnvVar(ENV_API_KEY, ""); // Empty string, not null
        SetEnvVar(ENV_VERSION, "2023-06-01");
        SetEnvVar(ENV_ATTRIBUTION, "0"); // Fix performance bug

        _logger.LogInformation("Configured Claude Code to use {BridgeUrl}", bridgeUrl);
    }

    public override async Task RestoreAsync()
    {
        await _backup.RestoreAsync(Name);
        _logger.LogInformation("Restored Claude Code configuration");
    }

    public override Task<bool> IsConfiguredAsync()
    {
        var baseUrl = GetEnvVar(ENV_BASE_URL);
        return Task.FromResult(!string.IsNullOrEmpty(baseUrl) && baseUrl.Contains("localhost:11434"));
    }

    public override Task LaunchAsync(string? model = null)
    {
        var args = model != null ? $"--model {model}" : "";
        var psi = new ProcessStartInfo
        {
            FileName = "claude",
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        Process.Start(psi);
        _logger.LogInformation("Launched Claude Code with model {Model}", model ?? "default");
        return Task.CompletedTask;
    }

    public override async Task<bool> IsRunningAsync()
    {
        return await IsProcessRunningAsync("claude");
    }

    public override async Task StopAsync()
    {
        await KillProcessAsync("claude");
    }

    private static async Task<string?> GetCommandPathAsync(string command)
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
            if (proc == null) return null;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Trim().Split('
').FirstOrDefault();
        }
        catch { return null; }
    }

    private static async Task<bool> IsProcessRunningAsync(string processName)
    {
        return await Task.Run(() => Process.GetProcessesByName(processName).Any());
    }

    private static async Task KillProcessAsync(string processName)
    {
        await Task.Run(() =>
        {
            foreach (var proc in Process.GetProcessesByName(processName))
            {
                try { proc.Kill(); proc.WaitForExit(5000); } catch { }
            }
        });
    }
}

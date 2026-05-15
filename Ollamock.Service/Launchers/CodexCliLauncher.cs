namespace Ollamock.Service.Launchers;

public class CodexCliLauncher : LauncherBase
{
    public override string Name => "codex";
    public override string DisplayName => "Codex CLI";
    public override string Description => "OpenAI Codex CLI - AI coding assistant";
    public override string ApiFormat => "openai";
    public override string Category => "cli";

    private const string ENV_BASE_URL = "OPENAI_BASE_URL";
    private const string ENV_API_KEY = "OPENAI_API_KEY";
    private const string ENV_MODEL = "OPENAI_MODEL";

    public CodexCliLauncher(IConfigBackup backup, ILogger<CodexCliLauncher> logger) : base(backup, logger) { }

    public override async Task<DetectResult> DetectAsync()
    {
        var exists = await CommandExistsAsync("codex");
        if (!exists) return new DetectResult(false, InstallHint: "npm install -g @openai/codex");

        var version = await GetCommandVersionAsync("codex", "--version");
        return new DetectResult(true, Version: version, Path: await GetCommandPathAsync("codex"));
    }

    public override Task<bool> IsInstallSupportedAsync() => Task.FromResult(true);

    public override async Task<InstallResult> InstallAsync()
    {
        return await RunCommandAsync("npm", "install -g @openai/codex", timeoutMs: 300000);
    }

    public override string? GetInstallCommand() => "npm install -g @openai/codex";
    public override string? GetHomepageUrl() => "https://github.com/openai/codex";
    public override string? GetDocumentationUrl() => "https://github.com/openai/codex/blob/main/README.md";

    public override async Task ConfigureAsync(string bridgeUrl, string? model = null)
    {
        var envVars = new Dictionary<string, string>
        {
            [ENV_BASE_URL] = GetEnvVar(ENV_BASE_URL) ?? "",
            [ENV_API_KEY] = GetEnvVar(ENV_API_KEY) ?? "",
            [ENV_MODEL] = GetEnvVar(ENV_MODEL) ?? ""
        };
        await _backup.BackupAsync(Name, envVars);

        // IMPORTANT: with /v1 for OpenAI format
        SetEnvVar(ENV_BASE_URL, $"{bridgeUrl.TrimEnd('/')}/v1");
        SetEnvVar(ENV_API_KEY, "ollama");
        if (model != null) SetEnvVar(ENV_MODEL, model);

        _logger.LogInformation("Configured Codex CLI to use {BridgeUrl}", bridgeUrl);
    }

    public override async Task RestoreAsync()
    {
        await _backup.RestoreAsync(Name);
    }

    public override Task<bool> IsConfiguredAsync()
    {
        var baseUrl = GetEnvVar(ENV_BASE_URL);
        return Task.FromResult(!string.IsNullOrEmpty(baseUrl) && baseUrl.Contains("localhost:11434"));
    }

    public override Task LaunchAsync(string? model = null)
    {
        var args = model != null ? $"-m {model}" : "--oss";
        var psi = new ProcessStartInfo
        {
            FileName = "codex",
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public override async Task<bool> IsRunningAsync()
    {
        return await IsProcessRunningAsync("codex");
    }

    public override async Task StopAsync()
    {
        await KillProcessAsync("codex");
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

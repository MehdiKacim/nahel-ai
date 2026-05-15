namespace Ollamock.Service.Launchers;

public class OpenCodeLauncher : LauncherBase
{
    public override string Name => "opencode";
    public override string DisplayName => "OpenCode";
    public override string Description => "OpenCode - Open-source AI coding assistant";
    public override string ApiFormat => "generic";
    public override string Category => "cli";

    private const string ENV_CONFIG = "OPENCODE_CONFIG_CONTENT";
    private readonly string _configPath;

    public OpenCodeLauncher(IConfigBackup backup, ILogger<OpenCodeLauncher> logger) : base(backup, logger)
    {
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "opencode", "opencode.json");
    }

    public override async Task<DetectResult> DetectAsync()
    {
        var exists = await CommandExistsAsync("opencode");
        if (!exists) return new DetectResult(false, InstallHint: "curl -fsSL https://opencode.ai/install | bash");

        var version = await GetCommandVersionAsync("opencode", "--version");
        return new DetectResult(true, Version: version);
    }

    public override Task<bool> IsInstallSupportedAsync() => Task.FromResult(true);

    public override async Task<InstallResult> InstallAsync()
    {
        return await RunCommandAsync("curl", "-fsSL https://opencode.ai/install | bash", timeoutMs: 300000);
    }

    public override string? GetInstallCommand() => "curl -fsSL https://opencode.ai/install | bash";
    public override string? GetHomepageUrl() => "https://opencode.ai";

    public override async Task ConfigureAsync(string bridgeUrl, string? model = null)
    {
        var envVars = new Dictionary<string, string>
        {
            [ENV_CONFIG] = GetEnvVar(ENV_CONFIG) ?? ""
        };
        await _backup.BackupAsync(Name, envVars, _configPath);

        var config = new
        {
            base_url = $"{bridgeUrl.TrimEnd('/')}/v1",
            api_key = "not-needed",
            model = model ?? "default"
        };

        SetEnvVar(ENV_CONFIG, System.Text.Json.JsonSerializer.Serialize(config));
        _logger.LogInformation("Configured OpenCode to use {BridgeUrl}", bridgeUrl);
    }

    public override async Task RestoreAsync()
    {
        await _backup.RestoreAsync(Name);
    }

    public override Task<bool> IsConfiguredAsync()
    {
        var config = GetEnvVar(ENV_CONFIG);
        return Task.FromResult(!string.IsNullOrEmpty(config) && config.Contains("localhost:11434"));
    }

    public override Task LaunchAsync(string? model = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "opencode",
            UseShellExecute = true
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public override async Task<bool> IsRunningAsync()
    {
        return await Task.Run(() => Process.GetProcessesByName("opencode").Any());
    }

    public override async Task StopAsync()
    {
        await Task.Run(() =>
        {
            foreach (var proc in Process.GetProcessesByName("opencode"))
            {
                try { proc.Kill(); proc.WaitForExit(5000); } catch { }
            }
        });
    }
}

namespace Ollamock.Service.Launchers;

public class DroidLauncher : LauncherBase
{
    public override string Name => "droid";
    public override string DisplayName => "Droid";
    public override string Description => "Droid by Factory AI - AI coding assistant";
    public override string ApiFormat => "generic";
    public override string Category => "cli";

    private readonly string _configPath;

    public DroidLauncher(IConfigBackup backup, ILogger<DroidLauncher> logger) : base(backup, logger)
    {
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".factory", "config.json");
    }

    public override async Task<DetectResult> DetectAsync()
    {
        var exists = await CommandExistsAsync("droid");
        if (!exists) return new DetectResult(false, InstallHint: "npm install -g @factoryai/droid");

        var version = await GetCommandVersionAsync("droid", "--version");
        return new DetectResult(true, Version: version);
    }

    public override Task<bool> IsInstallSupportedAsync() => Task.FromResult(true);

    public override async Task<InstallResult> InstallAsync()
    {
        return await RunCommandAsync("npm", "install -g @factoryai/droid", timeoutMs: 300000);
    }

    public override string? GetInstallCommand() => "npm install -g @factoryai/droid";
    public override string? GetHomepageUrl() => "https://factory.ai";

    public override async Task ConfigureAsync(string bridgeUrl, string? model = null)
    {
        var envVars = new Dictionary<string, string>();
        await _backup.BackupAsync(Name, envVars, _configPath);

        var config = new
        {
            custom_models = new[]
            {
                new
                {
                    model_display_name = $"{model ?? "default"} [Ollamock]",
                    model = model ?? "default",
                    base_url = $"{bridgeUrl.TrimEnd('/')}/v1/",
                    api_key = "not-needed",
                    provider = "generic-chat-completion-api",
                    max_tokens = 32000
                }
            }
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        await File.WriteAllTextAsync(_configPath, System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        _logger.LogInformation("Configured Droid to use {BridgeUrl}", bridgeUrl);
    }

    public override async Task RestoreAsync()
    {
        await _backup.RestoreAsync(Name);
    }

    public override Task<bool> IsConfiguredAsync()
    {
        return Task.FromResult(File.Exists(_configPath));
    }

    public override Task LaunchAsync(string? model = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "droid",
            UseShellExecute = true
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public override async Task<bool> IsRunningAsync()
    {
        return await Task.Run(() => Process.GetProcessesByName("droid").Any());
    }

    public override async Task StopAsync()
    {
        await Task.Run(() =>
        {
            foreach (var proc in Process.GetProcessesByName("droid"))
            {
                try { proc.Kill(); proc.WaitForExit(5000); } catch { }
            }
        });
    }
}

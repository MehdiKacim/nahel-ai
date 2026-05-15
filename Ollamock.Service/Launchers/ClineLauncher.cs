namespace Ollamock.Service.Launchers;

public class ClineLauncher : LauncherBase
{
    public override string Name => "cline";
    public override string DisplayName => "Cline";
    public override string Description => "Cline - VS Code extension for AI coding";
    public override string ApiFormat => "openai";
    public override string Category => "vscode";

    private readonly string _vscodeSettingsPath;

    public ClineLauncher(IConfigBackup backup, ILogger<ClineLauncher> logger) : base(backup, logger)
    {
        _vscodeSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Code", "User", "settings.json");
    }

    public override async Task<DetectResult> DetectAsync()
    {
        // Check VS Code is installed
        var vscodeExists = await CommandExistsAsync("code");
        if (!vscodeExists) return new DetectResult(false, InstallHint: "Install VS Code first, then Cline extension");

        // Check Cline extension
        var result = await RunCommandAsync("code", "--list-extensions", timeoutMs: 10000);
        if (!result.Success) return new DetectResult(false);

        var hasCline = result.Output?.Contains("saoudrizwan.claude-dev") ?? false;
        if (!hasCline) return new DetectResult(false, InstallHint: "Install Cline extension in VS Code");

        return new DetectResult(true, Version: "VS Code extension");
    }

    public override Task<bool> IsInstallSupportedAsync() => Task.FromResult(false);

    public override string? GetHomepageUrl() => "https://github.com/cline/cline";

    public override async Task ConfigureAsync(string bridgeUrl, string? model = null)
    {
        var envVars = new Dictionary<string, string>();
        await _backup.BackupAsync(Name, envVars, _vscodeSettingsPath);

        // Read existing settings
        var settings = new Dictionary<string, object>();
        if (File.Exists(_vscodeSettingsPath))
        {
            var json = await File.ReadAllTextAsync(_vscodeSettingsPath);
            settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
        }

        // Add Cline config
        settings["cline.api.baseUrl"] = $"{bridgeUrl.TrimEnd('/')}/v1";
        settings["cline.api.apiKey"] = "ollama";
        settings["cline.model"] = model ?? "ov-llama3.1";

        Directory.CreateDirectory(Path.GetDirectoryName(_vscodeSettingsPath)!);
        await File.WriteAllTextAsync(_vscodeSettingsPath, System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        _logger.LogInformation("Configured Cline in VS Code settings");
    }

    public override async Task RestoreAsync()
    {
        await _backup.RestoreAsync(Name);
    }

    public override Task<bool> IsConfiguredAsync()
    {
        if (!File.Exists(_vscodeSettingsPath)) return Task.FromResult(false);
        var json = File.ReadAllText(_vscodeSettingsPath);
        return Task.FromResult(json.Contains("cline.api.baseUrl") && json.Contains("localhost:11434"));
    }

    public override Task LaunchAsync(string? model = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "code",
            Arguments = "--new-window",
            UseShellExecute = true
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public override async Task<bool> IsRunningAsync()
    {
        return await Task.Run(() => Process.GetProcessesByName("Code").Any());
    }

    public override async Task StopAsync()
    {
        await Task.Run(() =>
        {
            foreach (var proc in Process.GetProcessesByName("Code"))
            {
                try { proc.CloseMainWindow(); proc.WaitForExit(5000); } catch { }
            }
        });
    }
}

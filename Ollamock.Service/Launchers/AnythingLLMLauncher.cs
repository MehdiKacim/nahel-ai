namespace Ollamock.Service.Launchers;

public class AnythingLLMLauncher : LauncherBase
{
    public override string Name => "anythingllm";
    public override string DisplayName => "AnythingLLM";
    public override string Description => "AnythingLLM - Desktop LLM interface";
    public override string ApiFormat => "openai";
    public override string Category => "desktop";

    private readonly string _appDataPath;

    public AnythingLLMLauncher(IConfigBackup backup, ILogger<AnythingLLMLauncher> logger) : base(backup, logger)
    {
        _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnythingLLM");
    }

    public override async Task<DetectResult> DetectAsync()
    {
        var exists = Directory.Exists(_appDataPath);
        if (!exists) return new DetectResult(false, InstallHint: "Download from https://anythingllm.com");

        // Check if executable exists
        var exePath = Path.Combine(_appDataPath, "AnythingLLM.exe");
        return File.Exists(exePath) 
            ? new DetectResult(true, Version: "desktop", Path: exePath)
            : new DetectResult(false, InstallHint: "Download AnythingLLM desktop app");
    }

    public override Task<bool> IsInstallSupportedAsync() => Task.FromResult(false);

    public override string? GetHomepageUrl() => "https://anythingllm.com";

    public override async Task ConfigureAsync(string bridgeUrl, string? model = null)
    {
        var envVars = new Dictionary<string, string>();
        await _backup.BackupAsync(Name, envVars);

        // AnythingLLM uses internal settings, we can only set env vars
        SetEnvVar("ANYTHING_LLM_API_BASE", $"{bridgeUrl.TrimEnd('/')}/v1");
        SetEnvVar("ANYTHING_LLM_API_KEY", "ollama");

        _logger.LogInformation("Configured AnythingLLM to use {BridgeUrl}", bridgeUrl);
    }

    public override async Task RestoreAsync()
    {
        await _backup.RestoreAsync(Name);
    }

    public override Task<bool> IsConfiguredAsync()
    {
        var baseUrl = GetEnvVar("ANYTHING_LLM_API_BASE");
        return Task.FromResult(!string.IsNullOrEmpty(baseUrl) && baseUrl.Contains("localhost:11434"));
    }

    public override Task LaunchAsync(string? model = null)
    {
        var exePath = Path.Combine(_appDataPath, "AnythingLLM.exe");
        if (!File.Exists(exePath))
        {
            _logger.LogWarning("AnythingLLM executable not found at {Path}", exePath);
            return Task.CompletedTask;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public override async Task<bool> IsRunningAsync()
    {
        return await Task.Run(() => Process.GetProcessesByName("AnythingLLM").Any());
    }

    public override async Task StopAsync()
    {
        await Task.Run(() =>
        {
            foreach (var proc in Process.GetProcessesByName("AnythingLLM"))
            {
                try { proc.CloseMainWindow(); proc.WaitForExit(5000); } catch { }
            }
        });
    }
}

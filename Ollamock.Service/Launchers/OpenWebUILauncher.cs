namespace Ollamock.Service.Launchers;

public class OpenWebUILauncher : LauncherBase
{
    public override string Name => "openwebui";
    public override string DisplayName => "OpenWebUI";
    public override string Description => "OpenWebUI - Web interface for LLMs";
    public override string ApiFormat => "openai";
    public override string Category => "web";

    public OpenWebUILauncher(IConfigBackup backup, ILogger<OpenWebUILauncher> logger) : base(backup, logger) { }

    public override async Task<DetectResult> DetectAsync()
    {
        var exists = await CommandExistsAsync("open-webui");
        if (!exists)
        {
            // Check if running as docker
            var dockerExists = await CommandExistsAsync("docker");
            if (dockerExists)
            {
                var result = await RunCommandAsync("docker", "ps --filter name=open-webui --format {{.Names}}", timeoutMs: 10000);
                if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
                    return new DetectResult(true, Version: "docker");
            }
            return new DetectResult(false, InstallHint: "pip install open-webui or docker run ghcr.io/open-webui/open-webui:main");
        }

        var version = await GetCommandVersionAsync("open-webui", "--version");
        return new DetectResult(true, Version: version);
    }

    public override Task<bool> IsInstallSupportedAsync() => Task.FromResult(true);

    public override async Task<InstallResult> InstallAsync()
    {
        return await RunCommandAsync("pip", "install open-webui", timeoutMs: 300000);
    }

    public override string? GetInstallCommand() => "pip install open-webui";
    public override string? GetHomepageUrl() => "https://openwebui.com";

    public override async Task ConfigureAsync(string bridgeUrl, string? model = null)
    {
        var envVars = new Dictionary<string, string>();
        await _backup.BackupAsync(Name, envVars);

        SetEnvVar("OPENAI_API_BASE_URL", $"{bridgeUrl.TrimEnd('/')}/v1");
        SetEnvVar("OPENAI_API_KEY", "ollama");
        SetEnvVar("ENABLE_OLLAMA_API", "False"); // Use OpenAI endpoint instead

        _logger.LogInformation("Configured OpenWebUI to use {BridgeUrl}", bridgeUrl);
    }

    public override async Task RestoreAsync()
    {
        await _backup.RestoreAsync(Name);
    }

    public override Task<bool> IsConfiguredAsync()
    {
        var baseUrl = GetEnvVar("OPENAI_API_BASE_URL");
        return Task.FromResult(!string.IsNullOrEmpty(baseUrl) && baseUrl.Contains("localhost:11434"));
    }

    public override Task LaunchAsync(string? model = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "open-webui",
            Arguments = "serve",
            UseShellExecute = true
        };
        Process.Start(psi);
        return Task.CompletedTask;
    }

    public override async Task<bool> IsRunningAsync()
    {
        // Check process or docker
        var processRunning = await Task.Run(() => Process.GetProcessesByName("open-webui").Any());
        if (processRunning) return true;

        var dockerResult = await RunCommandAsync("docker", "ps --filter name=open-webui --format {{.Names}}", timeoutMs: 10000);
        return dockerResult.Success && !string.IsNullOrWhiteSpace(dockerResult.Output);
    }

    public override async Task StopAsync()
    {
        await Task.Run(() =>
        {
            foreach (var proc in Process.GetProcessesByName("open-webui"))
            {
                try { proc.Kill(); proc.WaitForExit(5000); } catch { }
            }
        });
    }
}

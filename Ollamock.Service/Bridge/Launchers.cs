namespace Ollamock.Service.Bridge;

public class LauncherRegistry : ILauncherRegistry
{
    private readonly List<IToolLauncher> _launchers;

    public LauncherRegistry()
    {
        _launchers = new List<IToolLauncher>
        {
            new CodexLauncher(),
            new ClaudeCodeLauncher(),
            new OpenCodeLauncher(),
            new ClineLauncher(),
            new OpenWebUILauncher(),
            new AnythingLLMLauncher()
        };
    }

    public IEnumerable<IToolLauncher> GetAll() => _launchers;
    public IToolLauncher? Get(string name) => _launchers.FirstOrDefault(l => l.Name == name);
}

public abstract class ToolLauncherBase : IToolLauncher
{
    public abstract string Name { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }

    public virtual Task<bool> DetectAsync() => Task.FromResult(false);
    public virtual Task ConfigureAsync(string bridgeUrl) => Task.CompletedTask;
    public virtual Task LaunchAsync() => Task.CompletedTask;
    public virtual Task<bool> IsRunningAsync() => Task.FromResult(false);
    public virtual Task StopAsync() => Task.CompletedTask;

    protected static async Task<bool> CommandExists(string command)
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
}

public class CodexLauncher : ToolLauncherBase
{
    public override string Name => "codex";
    public override string DisplayName => "Codex App";
    public override string Description => "OpenAI Codex CLI";

    public override Task<bool> DetectAsync() => CommandExists("codex");
    public override Task ConfigureAsync(string bridgeUrl)
    {
        Environment.SetEnvironmentVariable("OPENAI_BASE_URL", $"{bridgeUrl}/v1", EnvironmentVariableTarget.User);
        return Task.CompletedTask;
    }
    public override Task LaunchAsync()
    {
        Process.Start(new ProcessStartInfo { FileName = "codex", UseShellExecute = true });
        return Task.CompletedTask;
    }
}

public class ClaudeCodeLauncher : ToolLauncherBase
{
    public override string Name => "claude";
    public override string DisplayName => "Claude Code";
    public override string Description => "Anthropic Claude Code CLI";

    public override Task<bool> DetectAsync() => CommandExists("claude");
    public override Task ConfigureAsync(string bridgeUrl)
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_BASE", $"{bridgeUrl}/v1", EnvironmentVariableTarget.User);
        return Task.CompletedTask;
    }
    public override Task LaunchAsync()
    {
        Process.Start(new ProcessStartInfo { FileName = "claude", UseShellExecute = true });
        return Task.CompletedTask;
    }
}

public class OpenCodeLauncher : ToolLauncherBase
{
    public override string Name => "opencode";
    public override string DisplayName => "OpenCode";
    public override string Description => "OpenCode CLI";
    public override Task<bool> DetectAsync() => CommandExists("opencode");
    public override Task LaunchAsync()
    {
        Process.Start(new ProcessStartInfo { FileName = "opencode", UseShellExecute = true });
        return Task.CompletedTask;
    }
}

public class ClineLauncher : ToolLauncherBase
{
    public override string Name => "cline";
    public override string DisplayName => "Cline";
    public override string Description => "Cline VS Code extension";
    public override Task<bool> DetectAsync() => Task.FromResult(true); // VS Code extension, always "available"
    public override Task LaunchAsync() => Task.CompletedTask; // Opens via VS Code
}

public class OpenWebUILauncher : ToolLauncherBase
{
    public override string Name => "openwebui";
    public override string DisplayName => "OpenWebUI";
    public override string Description => "OpenWebUI local interface";
    public override Task<bool> DetectAsync() => CommandExists("open-webui");
    public override Task LaunchAsync()
    {
        Process.Start(new ProcessStartInfo { FileName = "open-webui", UseShellExecute = true });
        return Task.CompletedTask;
    }
}

public class AnythingLLMLauncher : ToolLauncherBase
{
    public override string Name => "anythingllm";
    public override string DisplayName => "AnythingLLM";
    public override string Description => "AnythingLLM desktop";
    public override Task<bool> DetectAsync() => Task.FromResult(Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AnythingLLM")));
    public override Task LaunchAsync() => Task.CompletedTask;
}

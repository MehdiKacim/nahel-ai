using Microsoft.Extensions.Logging;

namespace Ollamock.Service.Launchers;

public class LauncherRegistry : ILauncherRegistry
{
    private readonly List<ILauncher> _launchers;

    public LauncherRegistry(IConfigBackup backup, ILoggerFactory loggerFactory)
    {
        _launchers = new List<ILauncher>
        {
            new ClaudeCodeLauncher(backup, loggerFactory.CreateLogger<ClaudeCodeLauncher>()),
            new CodexCliLauncher(backup, loggerFactory.CreateLogger<CodexCliLauncher>()),
            new OpenCodeLauncher(backup, loggerFactory.CreateLogger<OpenCodeLauncher>()),
            new DroidLauncher(backup, loggerFactory.CreateLogger<DroidLauncher>()),
            new ClineLauncher(backup, loggerFactory.CreateLogger<ClineLauncher>()),
            new OpenWebUILauncher(backup, loggerFactory.CreateLogger<OpenWebUILauncher>()),
            new AnythingLLMLauncher(backup, loggerFactory.CreateLogger<AnythingLLMLauncher>())
        };
    }

    public IEnumerable<ILauncher> GetAll() => _launchers;
    public ILauncher? Get(string name) => _launchers.FirstOrDefault(l => l.Name == name);
}

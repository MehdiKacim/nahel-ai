using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Ollamock.System.Launchers;

public sealed class ToolLauncherRegistry : IToolRegistry
{
    private readonly List<IToolLauncher> _launchers = new();

    public IReadOnlyList<IToolLauncher> GetAll() => _launchers;

    public IToolLauncher? Get(string toolId) => _launchers.FirstOrDefault(t => t.ToolId == toolId);

    public Task<IReadOnlyList<ToolInfo>> ListToolsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ToolInfo>>(_launchers.Select(t => new ToolInfo(t.ToolId, t.DisplayName, "tool", true)).ToList());

    public Task<ToolInfo?> GetToolAsync(string toolId, CancellationToken ct = default)
        => Task.FromResult<ToolInfo?>(_launchers.Select(t => new ToolInfo(t.ToolId, t.DisplayName, "tool", true)).FirstOrDefault(t => t.ToolId == toolId));
}

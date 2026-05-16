using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IToolLauncher
{
    string ToolId { get; }
    string DisplayName { get; }
    Task<ToolStatus> GetStatusAsync(CancellationToken ct = default);
    Task<ToolLaunchResult> StartAsync(ToolLaunchRequest request, CancellationToken ct = default);
    Task<ToolStopResult> StopAsync(ToolStopRequest request, CancellationToken ct = default);
}

using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IToolRegistry
{
    Task<IReadOnlyList<ToolInfo>> ListToolsAsync(CancellationToken ct = default);
    Task<ToolInfo?> GetToolAsync(string toolId, CancellationToken ct = default);
}

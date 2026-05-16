using Nahel.SDK.Models;

namespace Nahel.Engine.Ovms;

public sealed class OvmsVersionService
{
    public Task<EngineVersionInfo> GetVersionAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new EngineVersionInfo("ovms", null, null));
    }

    public Task<bool> DetectInstalledOvmsAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> DetectInstalledOpenVinoAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> VerifyOfficialRuntimeAsync(CancellationToken ct = default) => Task.FromResult(false);
}

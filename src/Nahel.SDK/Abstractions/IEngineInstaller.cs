using Nahel.SDK.Models;
namespace Nahel.SDK.Abstractions;
public interface IEngineInstaller
{
    Task<EngineInstallStatus> GetInstallStatusAsync(CancellationToken ct = default);
    Task<EngineInstallResult> InstallAsync(EngineInstallRequest request, CancellationToken ct = default);
    Task<EngineVerifyResult> VerifyAsync(CancellationToken ct = default);
}

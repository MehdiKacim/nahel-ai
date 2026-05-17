namespace Nahel.SDK.Abstractions;
public interface IBackendHealthClient
{
    Task<bool> IsHealthyAsync(string host, int port, CancellationToken ct = default);
}

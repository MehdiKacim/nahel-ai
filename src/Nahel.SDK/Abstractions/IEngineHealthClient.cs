namespace Nahel.SDK.Abstractions;
public interface IEngineHealthClient
{
    Task<bool> IsHealthyAsync(string host, int port, CancellationToken ct = default);
}

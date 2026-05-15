namespace Ollamock.Service.Providers;

public interface IRuntimeProvider
{
    string Type { get; }
    Task<bool> IsAvailableAsync();
    Task<ProviderHealth> HealthAsync();
}

public record ProviderHealth(bool Healthy, string? Error, double? LatencyMs);

public class OllamaProvider : IRuntimeProvider
{
    public string Type => "ollama";
    public Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public Task<ProviderHealth> HealthAsync() => Task.FromResult(new ProviderHealth(true, null, 0));
}

public class LlamaCppProvider : IRuntimeProvider
{
    public string Type => "llamacpp";
    public Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public Task<ProviderHealth> HealthAsync() => Task.FromResult(new ProviderHealth(true, null, 0));
}

public class OpenVinoProvider : IRuntimeProvider
{
    public string Type => "openvino";
    public Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public Task<ProviderHealth> HealthAsync() => Task.FromResult(new ProviderHealth(true, null, 0));
}

public class TEIProvider : IRuntimeProvider
{
    public string Type => "tei";
    public Task<bool> IsAvailableAsync() => Task.FromResult(true);
    public Task<ProviderHealth> HealthAsync() => Task.FromResult(new ProviderHealth(true, null, 0));
}

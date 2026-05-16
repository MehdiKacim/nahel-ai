namespace Nahel.Server.Configuration;

public sealed class NahelHostOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 11435;
    public bool DashboardEnabled { get; init; } = true;
    public bool OpenAiCompatibilityEnabled { get; init; } = true;
    public bool RequireApiKeyOnLan { get; init; } = true;
    public string ApiKey { get; init; } = "local";
    public bool AllowUnauthenticatedLan { get; init; } = false;
}

namespace Ollamock.Service.Bridge;

public interface IBackendLauncher
{
    Task<bool> EnsureRunningAsync(string providerId, string modelKey, CancellationToken ct = default);
    Task StopAsync(string providerId);
    Task<bool> IsHealthyAsync(string providerId, CancellationToken ct = default);
    string GetProviderUrl(string providerId);
    Process? GetProcess(string providerId);
    DateTime? GetStartTime(string providerId);
    int GetRestartCount(string providerId);
}

public interface IRouter
{
    string? ResolveProvider(string modelName);
    bool IsManaged(string modelName);
    bool SupportsGenerate(string modelName);
    bool SupportsEmbed(string modelName);
}

public interface IRequestAdapter
{
    Task<object> ToProviderFormatAsync(JsonElement ollamaRequest, string modelRealName, string providerType);
    IAsyncEnumerable<string> FromProviderStreamAsync(Stream providerStream, string modelName, CancellationToken ct);
}

public interface IAnthropicAdapter
{
    Task<object> ToOpenAiFormatAsync(JsonElement anthropicRequest, string modelRealName);
    Task<object> FromOpenAiFormatAsync(JsonElement openAiResponse, string modelName);
}

public interface ILauncherRegistry
{
    IEnumerable<ILauncher> GetAll();
    ILauncher? Get(string name);
}

public interface IConfigBackup
{
    Task BackupAsync(string toolName, Dictionary<string, string> envVars, string? filePath = null);
    Task RestoreAsync(string toolName);
    Task<bool> HasBackupAsync(string toolName);
}

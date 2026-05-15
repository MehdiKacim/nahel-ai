namespace OllamaBridge.Core;

public interface IRouter
{
    string? ResolveBackend(string modelName);
    bool IsManaged(string modelName);
    bool SupportsGenerate(string modelName);
    bool SupportsEmbed(string modelName);
}

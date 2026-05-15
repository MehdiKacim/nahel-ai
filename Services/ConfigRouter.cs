using OllamaBridge.Config;
using OllamaBridge.Core;
using Microsoft.Extensions.Options;

namespace OllamaBridge.Services;

public class ConfigRouter : IRouter
{
    private readonly AppSettings _config;

    public ConfigRouter(IOptions<AppSettings> config) => _config = config.Value;

    public string? ResolveBackend(string modelName)
    {
        if (!_config.Models.TryGetValue(modelName, out var model))
            return null;
        return model.BackendId;
    }

    public bool IsManaged(string modelName) => _config.Models.ContainsKey(modelName);

    public bool SupportsGenerate(string modelName)
    {
        if (!IsManaged(modelName)) return false;
        var backendId = ResolveBackend(modelName);
        if (backendId == null) return false;
        return _config.Backends.TryGetValue(backendId, out var backend) && backend.AdapterType != "embed";
    }

    public bool SupportsEmbed(string modelName)
    {
        if (!IsManaged(modelName)) return false;
        var backendId = ResolveBackend(modelName);
        if (backendId == null) return false;
        return _config.Backends.TryGetValue(backendId, out var backend) && backend.AdapterType == "embed";
    }
}

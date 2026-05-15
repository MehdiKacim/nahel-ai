using Microsoft.Extensions.Options;

namespace Ollamock.Service.Bridge;

public class ConfigRouter : IRouter
{
    private readonly ModelsConfig _models;
    private readonly ProvidersConfig _providers;

    public ConfigRouter(IOptions<ModelsConfig> models, IOptions<ProvidersConfig> providers)
    {
        _models = models.Value;
        _providers = providers.Value;
    }

    public string? ResolveProvider(string modelName)
    {
        if (!_models.TryGetValue(modelName, out var model))
            return null;
        return model.ProviderId;
    }

    public bool IsManaged(string modelName) => _models.ContainsKey(modelName);

    public bool SupportsGenerate(string modelName)
    {
        if (!IsManaged(modelName)) return false;
        var providerId = ResolveProvider(modelName);
        if (providerId == null) return false;
        return _providers.TryGetValue(providerId, out var provider) && provider.Type != "embed";
    }

    public bool SupportsEmbed(string modelName)
    {
        if (!IsManaged(modelName)) return false;
        var providerId = ResolveProvider(modelName);
        if (providerId == null) return false;
        return _providers.TryGetValue(providerId, out var provider) && provider.Type == "embed";
    }
}

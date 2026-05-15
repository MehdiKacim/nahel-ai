using OllamaBridge.Config;
using Microsoft.Extensions.Options;

namespace OllamaBridge.Services;

public class ModelRouter
{
    private readonly AppSettings _config;

    public ModelRouter(IOptions<AppSettings> config) => _config = config.Value;

    public bool IsManaged(string model) => _config.Models.ContainsKey(model);

    public (string backend, string backendUrl, string realName) Resolve(string model)
    {
        if (!_config.Models.TryGetValue(model, out var modelCfg))
            throw new InvalidOperationException($"Modele non configure: {model}");

        if (!_config.Backends.TryGetValue(modelCfg.Backend, out var backendCfg))
            throw new InvalidOperationException($"Backend non configure: {modelCfg.Backend}");

        return (modelCfg.Backend, $"http://localhost:{backendCfg.Port}", modelCfg.ModelPath);
    }

    public bool SupportsGenerate(string model) => _config.Routing.Generate.Contains(model);
    public bool SupportsEmbed(string model) => _config.Routing.Embed.Contains(model);
    public bool FallbackEnabled => _config.Routing.Fallback;
}

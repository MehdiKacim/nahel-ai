using Nahel.SDK.Policies;
using Nahel.Server.Configuration;

namespace Nahel.Server.Services;

public sealed class NahelBootstrapService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NahelBootstrapService> _logger;

    public NahelBootstrapService(IServiceProvider services, IConfiguration configuration, ILogger<NahelBootstrapService> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var config = _configuration.GetSection("Nahel").Get<NahelHostOptions>();
        _logger.LogInformation("Nahel bootstrap starting...");

        var catalog = _services.GetRequiredService<IEngineCatalog>();
        var modelRouter = _services.GetRequiredService<IModelRouter>();
        var runtime = _services.GetRequiredService<IEngineRuntime>();

        // Register all engines from DI into catalog
        foreach (var engine in _services.GetServices<Nahel.SDK.Abstractions.IEngine>())
        {
            catalog.RegisterEngine(engine);
            _logger.LogInformation("Registered engine '{EngineId}' in catalog.", engine.EngineId);
        }

        // Register models from config
        var modelsSection = _configuration.GetSection("Models");
        foreach (var modelEntry in modelsSection.GetChildren())
        {
            var modelCfg = modelEntry.Get<Configuration.ModelConfigEntry>();
            if (modelCfg == null || !modelCfg.Enabled) continue;

            modelRouter.Register(modelEntry.Key, modelCfg.EngineId, modelCfg.EngineModelName);
            _logger.LogInformation("Registered model '{ModelId}' -> engine '{EngineId}'", modelEntry.Key, modelCfg.EngineId);
        }

        // Auto-start engines with OnServerStart policy
        var enginesSection = _configuration.GetSection("Engines");
        foreach (var engineEntry in enginesSection.GetChildren())
        {
            var engineCfg = engineEntry.Get<Configuration.EngineConfigEntry>();
            if (engineCfg == null || !engineCfg.Enabled) continue;
            if (engineCfg.AutoStartPolicy != EngineAutoStartPolicy.OnServerStart) continue;

            var engine = catalog.GetEngine(engineEntry.Key);
            if (engine == null)
            {
                _logger.LogWarning("Engine '{EngineId}' configured for auto-start but not registered in catalog.", engineEntry.Key);
                continue;
            }

            try
            {
                _logger.LogInformation("Auto-starting engine '{EngineId}'...", engineEntry.Key);
                await runtime.StartAsync(engineEntry.Key, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-start engine '{EngineId}'", engineEntry.Key);
            }
        }

        _logger.LogInformation("Nahel bootstrap complete.");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

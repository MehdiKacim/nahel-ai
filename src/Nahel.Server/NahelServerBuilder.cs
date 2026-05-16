using Nahel.Engine.Ovms;
using Nahel.Ollamock.System.Backup;
using Nahel.Ollamock.System.Launchers;
using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;
using Nahel.Server.Configuration;
using Nahel.Server.Dashboard;
using Nahel.Server.Hubs;
using Nahel.Server.Routes;
using Nahel.Server.Services;

namespace Nahel.Server;

public static class NahelServerBuilder
{
    public static IServiceCollection AddNahelServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEngineCatalog, EngineCatalog>();
        services.AddSingleton<IEngineRuntime, EngineRuntime>();
        services.AddSingleton<IEngineCommandQueue, EngineCommandQueue>();
        services.AddHostedService(sp => (EngineCommandQueue)sp.GetRequiredService<IEngineCommandQueue>());
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        services.AddSingleton<IEngineEventBus, EngineEventBus>();
        services.AddSingleton<IModelRouter, ModelRouter>();
        services.AddSingleton<IOpenAiRouter, OpenAiRouter>();
        services.AddHostedService<NahelBootstrapService>();

        // Ollamock.System services
        services.AddSingleton<IConfigBackup, ConfigBackup>();
        services.AddSingleton<ToolLauncherRegistry>();
        services.AddSingleton<IToolRegistry>(sp => sp.GetRequiredService<ToolLauncherRegistry>());

        // Register engines from config
        var enginesSection = configuration.GetSection("Engines");
        foreach (var engineEntry in enginesSection.GetChildren())
        {
            var engineCfg = engineEntry.Get<EngineConfigEntry>();
            if (engineCfg == null || !engineCfg.Enabled) continue;

            var engineId = engineEntry.Key;

            if (engineCfg.Type.Equals("ovms", StringComparison.OrdinalIgnoreCase))
            {
                var ovmsOptions = new OvmsOptions
                {
                    EngineId = engineId,
                    DisplayName = engineCfg.DisplayName,
                    ExecutablePath = engineCfg.ExecutablePath,
                    WorkingDirectory = engineCfg.WorkingDirectory,
                    ConfigPath = engineCfg.ConfigPath,
                    RestPort = engineCfg.RestPort,
                    GrpcPort = engineCfg.GrpcPort,
                    OpenAiProxyPort = engineCfg.OpenAiProxyPort,
                    OpenVinoVersion = engineCfg.OpenVinoVersion,
                    VersionPolicy = engineCfg.VersionPolicy,
                    EnvironmentVariables = engineCfg.EnvironmentVariables
                };
                services.AddSingleton(ovmsOptions);
                services.AddSingleton<OvmsProcessSupervisor>();
                services.AddHttpClient<OvmsHealthClient>();
                services.AddSingleton<OvmsConfigWriter>();
                services.AddSingleton<OvmsModelRegistry>();
                services.AddSingleton<OvmsModelSwitcher>();
                services.AddSingleton<OvmsVersionService>();
                services.AddSingleton<OvmsEngine>();
                services.AddSingleton<IEngine>(sp => sp.GetRequiredService<OvmsEngine>());
            }
            // Future engine types (llama.cpp, ollama, etc.) registered here
        }

        // Register models from config into IModelRegistry of each engine
        var modelsSection = configuration.GetSection("Models");
        foreach (var modelEntry in modelsSection.GetChildren())
        {
            var modelCfg = modelEntry.Get<ModelConfigEntry>();
            if (modelCfg == null || !modelCfg.Enabled) continue;

            // We can't inject IModelRegistry here at config-time easily,
            // so models are registered at runtime by NahelBootstrapService via ModelRouter.
            // Engine-specific registries will be populated on first use.
        }

        services.AddSignalR();
        return services;
    }

    public static IApplicationBuilder UseNahelServer(this IApplicationBuilder app)
    {
        app.UseMiddleware<DashboardMiddleware>();
        return app;
    }

    public static IEndpointRouteBuilder MapNahelRoutes(this IEndpointRouteBuilder app)
    {
        app.MapNativeRoutes();
        app.MapOpenAiRoutes();
        app.MapOllamaRoutes();
        app.MapHub<EngineHub>(Nahel.SDK.Routes.NahelRoutes.SignalREngineHub);
        return app;
    }
}

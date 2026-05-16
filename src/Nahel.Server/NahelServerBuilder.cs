using Nahel.Engine.Ovms;
using Nahel.Ollamock.System.Backup;
using Nahel.Ollamock.System.Launchers;
using Nahel.SDK.Abstractions;
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

        // Ollamock.System services
        services.AddSingleton<IConfigBackup, ConfigBackup>();
        services.AddSingleton<ToolLauncherRegistry>();
        services.AddSingleton<IToolRegistry>(sp => sp.GetRequiredService<ToolLauncherRegistry>());

        // OVMS services (register options from config section "Engines:ovms")
        services.Configure<OvmsOptions>(configuration.GetSection("Engines:ovms"));
        services.AddSingleton<OvmsProcessSupervisor>();
        services.AddHttpClient<OvmsHealthClient>();
        services.AddSingleton<OvmsConfigWriter>();
        services.AddSingleton<OvmsModelRegistry>();
        services.AddSingleton<OvmsModelSwitcher>();
        services.AddSingleton<OvmsVersionService>();
        services.AddSingleton<OvmsEngine>();
        services.AddSingleton<IEngine>(sp => sp.GetRequiredService<OvmsEngine>());

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

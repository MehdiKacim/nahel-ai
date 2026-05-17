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
        services.AddSingleton<IBackendCatalog, BackendCatalog>();
        services.AddSingleton<IBackendRuntime, BackendRuntime>();
        services.AddSingleton<IBackendCommandQueue, BackendCommandQueue>();
        services.AddHostedService(sp => (BackendCommandQueue)sp.GetRequiredService<IBackendCommandQueue>());
        services.AddSingleton<IJobStore, InMemoryJobStore>();
        services.AddSingleton<IBackendEventBus, BackendEventBus>();
        services.AddSingleton<IModelRouter, ModelRouter>();
        services.AddSingleton<IOpenAiRouter, OpenAiRouter>();
        services.AddHostedService<NahelBootstrapService>();

        // Ollamock.System services
        services.AddSingleton<IConfigBackup, ConfigBackup>();
        services.AddSingleton<ToolLauncherRegistry>();
        services.AddSingleton<IToolRegistry>(sp => sp.GetRequiredService<ToolLauncherRegistry>());

        // Common infrastructure for backends
        services.AddHttpClient();

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
        app.MapManagementRoutes();
        app.MapHub<EngineHub>(Nahel.SDK.Routes.NahelRoutes.SignalREngineHub);
        return app;
    }
}

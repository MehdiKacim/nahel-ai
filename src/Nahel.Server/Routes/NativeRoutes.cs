using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;
using Nahel.SDK.Routes;
using Nahel.Server.Services;

namespace Nahel.Server.Routes;

public static class NativeRoutes
{
    public static IEndpointRouteBuilder MapNativeRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet(NahelRoutes.Health, () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));
        app.MapGet(NahelRoutes.Version, () => Results.Ok(new { version = "0.1.0-nahel" }));

        app.MapGet(NahelRoutes.Engines, (IBackendCatalog catalog) => Results.Ok(catalog.GetBackends().Select(e => new { e.EngineId, e.DisplayName, e.EngineType })));

        app.MapGet(NahelRoutes.EngineStatus, async (string engineId, IBackendCatalog catalog) =>
        {
            var engine = catalog.GetBackend(engineId);
            return engine == null ? Results.NotFound() : Results.Ok(await engine.GetStatusAsync());
        });

        app.MapGet(NahelRoutes.EngineHealth, async (string engineId, IBackendCatalog catalog) =>
        {
            var engine = catalog.GetBackend(engineId);
            return engine == null ? Results.NotFound() : Results.Ok(await engine.GetHealthAsync());
        });

        app.MapPost(NahelRoutes.EngineStart, async (string engineId, IBackendRuntime runtime) =>
        {
            var result = await runtime.StartAsync(engineId);
            return Results.Accepted($"/jobs/{result.JobId}", new { jobId = result.JobId, status = "queued" });
        });
        app.MapPost(NahelRoutes.EngineStop, async (string engineId, IBackendRuntime runtime) =>
        {
            var result = await runtime.StopAsync(engineId);
            return Results.Accepted($"/jobs/{result.JobId}", new { jobId = result.JobId, status = "queued" });
        });
        app.MapPost(NahelRoutes.EngineRestart, async (string engineId, IBackendRuntime runtime) =>
        {
            var result = await runtime.RestartAsync(engineId);
            return Results.Accepted($"/jobs/{result.JobId}", new { jobId = result.JobId, status = "queued" });
        });

        app.MapGet(NahelRoutes.EngineCapabilities, async (string engineId, IBackendCatalog catalog) =>
        {
            var engine = catalog.GetBackend(engineId);
            return engine == null ? Results.NotFound() : Results.Ok(await engine.GetCapabilitiesAsync());
        });

        app.MapGet(NahelRoutes.EngineModels, async (string engineId, IBackendCatalog catalog) =>
        {
            var engine = catalog.GetBackend(engineId);
            return engine == null ? Results.NotFound() : Results.Ok(await engine.ListModelsAsync());
        });

        app.MapPost(NahelRoutes.EngineSwitchModel, async (string engineId, ModelSwitchRequest request, IBackendCatalog catalog) =>
        {
            var engine = catalog.GetBackend(engineId);
            return engine == null ? Results.NotFound() : Results.Ok(await engine.SwitchModelAsync(request));
        });

        app.MapGet(NahelRoutes.Jobs, (IBackendCommandQueue queue) => Results.Ok(queue.GetJobs()));
        app.MapGet(NahelRoutes.JobById, (string jobId, IBackendCommandQueue queue) =>
        {
            var job = queue.GetJobAsync(jobId).Result;
            return job == null ? Results.NotFound() : Results.Ok(job);
        });

        app.MapGet(NahelRoutes.Tools, (Nahel.Ollamock.System.Launchers.ToolLauncherRegistry registry) => Results.Ok(registry.GetAll().Select(t => new { t.ToolId, t.DisplayName })));
        app.MapGet(NahelRoutes.ToolStatus, async (string toolId, Nahel.Ollamock.System.Launchers.ToolLauncherRegistry registry) =>
        {
            var tool = registry.Get(toolId);
            return tool == null ? Results.NotFound() : Results.Ok(await tool.GetStatusAsync());
        });
        app.MapPost(NahelRoutes.ToolStart, async (string toolId, ToolLaunchRequest request, Nahel.Ollamock.System.Launchers.ToolLauncherRegistry registry) =>
        {
            var tool = registry.Get(toolId);
            return tool == null ? Results.NotFound() : Results.Ok(await tool.StartAsync(request));
        });
        app.MapPost(NahelRoutes.ToolStop, async (string toolId, ToolStopRequest request, Nahel.Ollamock.System.Launchers.ToolLauncherRegistry registry) =>
        {
            var tool = registry.Get(toolId);
            return tool == null ? Results.NotFound() : Results.Ok(await tool.StopAsync(request));
        });

        return app;
    }
}

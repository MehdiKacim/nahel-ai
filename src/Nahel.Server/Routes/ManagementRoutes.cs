using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;
using Nahel.SDK.Routes;
using Nahel.Server.Services;

namespace Nahel.Server.Routes;

public static class ManagementRoutes
{
    public static IEndpointRouteBuilder MapManagementRoutes(this IEndpointRouteBuilder app)
    {
        // Status / health aggregation
        app.MapGet("/api/status", async (IBackendCatalog catalog, IModelRouter router) =>
        {
            var backends = new List<object>();
            foreach (var b in catalog.GetBackends())
            {
                backends.Add(new
                {
                    b.EngineId,
                    b.DisplayName,
                    b.EngineType,
                    state = (await b.GetStatusAsync()).State
                });
            }
            return Results.Ok(new
            {
                server = "nahel",
                version = "0.2.0",
                backends,
                models = router.ListModels()
            });
        });

        // Model listing (CLI: nahel models list)
        app.MapGet("/models", async (IBackendCatalog catalog) =>
        {
            var items = new List<object>();
            foreach (var b in catalog.GetBackends())
            {
                var models = await b.ListModelsAsync();
                foreach (var m in models)
                {
                    items.Add(new
                    {
                        m.ModelId,
                        m.DisplayName,
                        m.BackendId,
                        m.BackendModelName,
                        m.ModelPath,
                        backendType = b.EngineType,
                        state = (await b.GetStatusAsync()).State
                    });
                }
            }
            return Results.Ok(new { models = items });
        });

        // Model download (CLI: nahel models download <repo>)
        app.MapPost("/models/download", async (DownloadModelRequest request, IBackendCommandQueue queue) =>
        {
            try
            {
                var localDir = Path.Combine("models", request.RepoId.Replace('/', '_'));
                var result = await queue.EnqueueAsync(new JobRequest(
                    JobType.DownloadModel,
                    null,
                    request.ModelId,
                    new Dictionary<string, string>
                    {
                        ["repo_id"] = request.RepoId,
                        ["local_dir"] = localDir
                    }));

                return Results.Accepted($"/jobs/{result.JobId}", new
                {
                    modelId = request.ModelId,
                    repo = request.RepoId,
                    localDir,
                    jobId = result.JobId,
                    status = "queued"
                });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to queue download: {ex.Message}");
            }
        });

        // Model switch (CLI: nahel models switch <model> or nahel run <model>)
        app.MapPost("/models/switch", async (SwitchModelRequest request, IBackendCatalog catalog, IModelRouter router, IConfiguration configuration) =>
        {
            var backendId = request.BackendId;
            if (string.IsNullOrEmpty(backendId))
            {
                var resolved = router.ResolveModel(request.ModelId);
                if (resolved == null)
                    return Results.NotFound(new { error = $"Model '{request.ModelId}' not found." });
                backendId = resolved.Value.engineId;
            }

            var backend = catalog.GetBackends().FirstOrDefault(b => b.EngineId == backendId);
            if (backend == null)
                return Results.NotFound(new { error = $"Backend '{backendId}' not found." });

            var modelPath = request.TargetEngineModelName ?? configuration.GetSection($"models:{request.ModelId}:path").Value;
            var result = await backend.SwitchModelAsync(new ModelSwitchRequest(request.ModelId, modelPath), CancellationToken.None);
            if (result.Success)
                return Results.Ok(new { success = true, modelId = request.ModelId });
            return Results.BadRequest(new { success = false, error = result.Message });
        });

        // Bench (CLI: nahel bench <model>)
        app.MapPost("/bench", async (BenchRequest request, IBackendCatalog catalog, IModelRouter router) =>
        {
            var resolved = router.ResolveModel(request.ModelId);
            if (resolved == null)
                return Results.NotFound(new { error = $"Model '{request.ModelId}' not found." });

            var backend = catalog.GetBackend(resolved.Value.engineId);
            if (backend == null)
                return Results.NotFound(new { error = $"Backend for model '{request.ModelId}' not found." });

            var openAiBackend = backend as Nahel.SDK.Abstractions.IOpenAiBackend;
            if (openAiBackend == null)
                return Results.BadRequest(new { error = "Backend does not support OpenAI-compatible benchmarking." });

            var maxTokens = request.MaxTokens ?? 64;
            var prompt = new Nahel.SDK.Models.OpenAiChatCompletionRequest(
                Model: request.ModelId,
                Messages: new List<Nahel.SDK.Models.OpenAiChatMessage>
                {
                    new("system", "You are a helpful assistant."),
                    new("user", "What is the capital of France?")
                },
                MaxTokens: maxTokens,
                Temperature: 0.7
            );

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var response = await openAiBackend.CreateChatCompletionAsync(prompt, CancellationToken.None);
                sw.Stop();
                var choice = response.Choices?.FirstOrDefault();
                var text = (choice?.Message as Nahel.SDK.Models.OpenAiChatMessage)?.Content?.ToString() ?? "";
                var completionTokens = response.Usage?.CompletionTokens ?? 0;
                var totalTokens = response.Usage?.TotalTokens ?? 0;
                var durationSec = sw.ElapsedMilliseconds / 1000.0;
                return Results.Ok(new
                {
                    model = request.ModelId,
                    duration_ms = sw.ElapsedMilliseconds,
                    completion_tokens = completionTokens,
                    total_tokens = totalTokens,
                    tokens_per_second = durationSec > 0 ? Math.Round(completionTokens / durationSec, 2) : 0,
                    preview = text.Length > 200 ? text[..200] : text
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = $"Benchmark failed: {ex.Message}" });
            }
        });

        // Metrics (CLI: nahel metrics)
        app.MapGet("/metrics", async (IBackendCatalog catalog) =>
        {
            var items = new List<object>();
            foreach (var b in catalog.GetBackends())
            {
                var health = await b.GetHealthAsync();
                items.Add(new
                {
                    backendId = b.EngineId,
                    healthy = health.Reachable,
                    detail = health.StatusMessage
                });
            }
            return Results.Ok(new { backends = items });
        });

        // Model removal (CLI: nahel models rm <model-id>)
        app.MapDelete("/models/{modelId}", async (string modelId, IBackendCatalog catalog, IModelRouter router, IBackendRuntime runtime) =>
        {
            var resolved = router.ResolveModel(modelId);
            if (resolved == null)
                return Results.NotFound(new { error = $"Model '{modelId}' not found." });

            var backend = catalog.GetBackend(resolved.Value.engineId);
            if (backend != null)
            {
                try { await runtime.StopAsync(backend.EngineId); } catch { /* ignore */ }
                catalog.UnregisterBackend(backend.EngineId);
            }
            router.Unregister(modelId);
            return Results.Ok(new { success = true, message = $"Model '{modelId}' removed." });
        });

        app.MapGet("/api/models/downloads", () => Results.Ok(new { downloads = new List<object>() }));

        return app;
    }
}

public sealed record DownloadModelRequest(string ModelId, string RepoId, string? Backend = "ovgenai", string? Device = "CPU");
public sealed record SwitchModelRequest(string ModelId, string BackendId, string? TargetEngineModelName);
public sealed record BenchRequest(string ModelId, int? MaxTokens = null);

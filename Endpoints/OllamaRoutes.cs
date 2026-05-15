using OllamaBridge.Config;
using OllamaBridge.Core;
using OllamaBridge.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace OllamaBridge.Endpoints;

public static class OllamaRouteExtensions
{
    public static IApplicationBuilder MapOllamaRoutes(this IApplicationBuilder app)
    {
        var router = app.ApplicationServices.GetRequiredService<IRouter>();
        var launcher = app.ApplicationServices.GetRequiredService<IBackendLauncher>();
        var adapter = app.ApplicationServices.GetRequiredService<IRequestAdapter>();
        var httpFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();
        var logSink = app.ApplicationServices.GetRequiredService<LogSink>();
        var metrics = app.ApplicationServices.GetRequiredService<MetricsSink>();
        var config = app.ApplicationServices.GetRequiredService<IOptions<AppSettings>>().Value;

        var builder = app.New();

        // ============ /api/generate ============
        builder.MapPost("/api/generate", async ([FromBody] JsonElement body, HttpContext ctx, CancellationToken ct) =>
        {
            var reqId = Guid.NewGuid().ToString("N")[..8];
            ctx.Response.Headers.Append("X-Request-Id", reqId);
            var model = body.GetProperty("model").GetString() ?? "";

            if (!router.IsManaged(model))
                return await ProxyToOllamaNative(httpFactory, config, body, "/api/generate", ct);

            if (!router.SupportsGenerate(model))
                return Results.BadRequest(new { error = "Modele non supporte pour generate" });

            var backendId = router.ResolveBackend(model)!;
            var modelConfig = config.Models[model];

            var started = await launcher.EnsureRunningAsync(backendId, model, ct);
            if (!started) return Results.StatusCode(503);

            var backendUrl = launcher.GetBackendUrl(backendId);
            var backendType = config.Backends[backendId].AdapterType;
            var openAiBody = await adapter.ToBackendFormatAsync(body, modelConfig.ModelPath, backendType);

            var client = httpFactory.CreateClient("backend");
            var sw = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync($"{backendUrl}/v1/chat/completions", openAiBody, ct);
            var responseStream = await response.Content.ReadAsStreamAsync(ct);

            var proc = (launcher as BackendLauncher)?.GetProcess(backendId);
            var ramMb = proc != null ? proc.WorkingSet64 / (1024 * 1024) : 0;
            var device = modelConfig.Device ?? "AUTO";

            return Results.Stream(async stream =>
            {
                await using var writer = new StreamWriter(stream);
                var tokenCount = 0;
                var firstToken = true;
                var ttft = 0.0;

                await foreach (var chunk in adapter.FromBackendStreamAsync(responseStream, model, ct))
                {
                    await writer.WriteLineAsync(chunk);
                    await writer.FlushAsync(ct);

                    try
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(chunk);
                        if (data.TryGetProperty("response", out var resp) && resp.GetString()?.Length > 0)
                        {
                            if (firstToken) { ttft = sw.Elapsed.TotalMilliseconds; firstToken = false; }
                            tokenCount++;
                        }
                    }
                    catch { }
                }

                sw.Stop();
                var tokS = tokenCount > 0 ? tokenCount / (sw.ElapsedMilliseconds / 1000.0) : 0;
                metrics.Record(reqId, model, backendId, device, ttft, tokS, ramMb, sw.ElapsedMilliseconds);
                logSink.Write("INF", $"[req-{reqId}] {model} | {tokenCount}tok | {tokS:F1}tok/s | {sw.ElapsedMilliseconds}ms");
            }, "application/x-ndjson");
        });

        // ============ /api/embed ============
        builder.MapPost("/api/embed", async ([FromBody] JsonElement body, HttpContext ctx, CancellationToken ct) =>
        {
            var reqId = Guid.NewGuid().ToString("N")[..8];
            ctx.Response.Headers.Append("X-Request-Id", reqId);
            var model = body.GetProperty("model").GetString() ?? "";

            if (!router.IsManaged(model))
                return await ProxyToOllamaNative(httpFactory, config, body, "/api/embed", ct);

            if (!router.SupportsEmbed(model))
                return Results.BadRequest(new { error = "Modele non supporte pour embed" });

            var backendId = router.ResolveBackend(model)!;
            var modelConfig = config.Models[model];

            var started = await launcher.EnsureRunningAsync(backendId, model, ct);
            if (!started) return Results.StatusCode(503);

            var backendUrl = launcher.GetBackendUrl(backendId);
            var backendType = config.Backends[backendId].AdapterType;
            var embedBody = await adapter.ToBackendFormatAsync(body, modelConfig.ModelPath, backendType);

            var client = httpFactory.CreateClient("backend");
            var sw = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync($"{backendUrl}/v1/embeddings", embedBody, ct);
            sw.Stop();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var embeddings = result.GetProperty("data").EnumerateArray()
                .Select(d => d.GetProperty("embedding").EnumerateArray().Select(x => x.GetDouble()).ToArray())
                .ToList();

            var proc = (launcher as BackendLauncher)?.GetProcess(backendId);
            var ramMb = proc != null ? proc.WorkingSet64 / (1024 * 1024) : 0;
            var device = modelConfig.Device ?? "AUTO";

            metrics.Record(reqId, model, backendId, device, sw.Elapsed.TotalMilliseconds, 0, ramMb, sw.ElapsedMilliseconds);
            logSink.Write("INF", $"[req-{reqId}] {model} embed | {sw.ElapsedMilliseconds}ms");

            return Results.Json(new { model, embeddings, total_duration = sw.ElapsedMilliseconds });
        });

        // ============ /v1/chat/completions (OpenAI compatible) ============
        builder.MapPost("/v1/chat/completions", async ([FromBody] JsonElement body, HttpContext ctx, CancellationToken ct) =>
        {
            var reqId = Guid.NewGuid().ToString("N")[..8];
            ctx.Response.Headers.Append("X-Request-Id", reqId);
            var model = body.GetProperty("model").GetString() ?? "";

            if (!router.IsManaged(model))
                return await ProxyToOllamaNative(httpFactory, config, body, "/v1/chat/completions", ct);

            var backendId = router.ResolveBackend(model)!;
            var modelConfig = config.Models[model];

            var started = await launcher.EnsureRunningAsync(backendId, model, ct);
            if (!started) return Results.StatusCode(503);

            var backendUrl = launcher.GetBackendUrl(backendId);
            var client = httpFactory.CreateClient("backend");
            var response = await client.PostAsJsonAsync($"{backendUrl}/v1/chat/completions", body, ct);

            return Results.Stream(await response.Content.ReadAsStreamAsync(ct), response.Content.Headers.ContentType?.ToString() ?? "application/json");
        });

        // ============ /api/tags ============
        builder.MapGet("/api/tags", async (CancellationToken ct) =>
        {
            var client = httpFactory.CreateClient();
            var native = await client.GetFromJsonAsync<JsonElement>($"{config.Bridge.OllamaNative}/api/tags", ct);

            var models = native.TryGetProperty("models", out var m)
                ? m.Deserialize<List<JsonElement>>() ?? new()
                : new List<JsonElement>();

            foreach (var (name, cfg) in config.Models)
            {
                var backend = config.Backends[cfg.BackendId];
                models.Add(JsonSerializer.SerializeToElement(new
                {
                    name,
                    model = name,
                    modified_at = DateTime.UtcNow.ToString("o"),
                    size = 0L,
                    digest = $"ov-{name}-{cfg.BackendId}",
                    details = new { family = cfg.BackendId, parameter_size = cfg.ContextSize?.ToString() ?? "?" }
                }));
            }

            return Results.Json(new { models });
        });

        // ============ /api/version ============
        builder.MapGet("/api/version", () => Results.Json(new { version = "0.5.0-ov-bridge" }));

        return builder.Build();
    }

    private static async Task<IResult> ProxyToOllamaNative(IHttpClientFactory factory, AppSettings config, JsonElement body, string path, CancellationToken ct)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync($"{config.Bridge.OllamaNative}{path}", body, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(content, response.Content.Headers.ContentType?.ToString(), statusCode: (int)response.StatusCode);
    }
}

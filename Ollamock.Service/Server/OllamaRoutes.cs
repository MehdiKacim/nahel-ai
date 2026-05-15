using System.Diagnostics;
using System.Text.Json;

namespace Ollamock.Service.Server;

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
        var bridgeConfig = app.ApplicationServices.GetRequiredService<IOptions<BridgeConfig>>().Value;
        var modelsConfig = app.ApplicationServices.GetRequiredService<IOptions<ModelsConfig>>().Value;
        var providersConfig = app.ApplicationServices.GetRequiredService<IOptions<ProvidersConfig>>().Value;

        var builder = app.New();

        // /api/generate
        builder.MapPost("/api/generate", async ([FromBody] JsonElement body, HttpContext ctx, CancellationToken ct) =>
        {
            var reqId = Guid.NewGuid().ToString("N")[..8];
            ctx.Response.Headers.Append("X-Request-Id", reqId);
            var model = body.GetProperty("model").GetString() ?? "";

            if (!router.IsManaged(model))
                return await ProxyToNative(httpFactory, bridgeConfig, body, "/api/generate", ct);

            if (!router.SupportsGenerate(model))
                return Results.BadRequest(new { error = "Modele non supporte pour generate" });

            var providerId = router.ResolveProvider(model)!;
            var modelEntry = modelsConfig[model];

            var started = await launcher.EnsureRunningAsync(providerId, model, ct);
            if (!started) return Results.StatusCode(503);

            var providerUrl = launcher.GetProviderUrl(providerId);
            var providerType = providersConfig[providerId].Type;
            var openAiBody = await adapter.ToProviderFormatAsync(body, modelEntry.ModelPath, providerType);

            var client = httpFactory.CreateClient("backend");
            var sw = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync($"{providerUrl}/v1/chat/completions", openAiBody, ct);
            var responseStream = await response.Content.ReadAsStreamAsync(ct);

            var proc = launcher.GetProcess(providerId);
            var ramMb = proc != null ? proc.WorkingSet64 / (1024 * 1024) : 0;
            var device = modelEntry.Device ?? "AUTO";

            return Results.Stream(async stream =>
            {
                await using var writer = new StreamWriter(stream);
                var tokenCount = 0;
                var firstToken = true;
                var ttft = 0.0;

                await foreach (var chunk in adapter.FromProviderStreamAsync(responseStream, model, ct))
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
                metrics.Record(reqId, model, providerId, device, ttft, tokS, ramMb, sw.ElapsedMilliseconds);
                logSink.Write("INF", $"[req-{reqId}] {model} | {tokenCount}tok | {tokS:F1}tok/s | {sw.ElapsedMilliseconds}ms");
            }, "application/x-ndjson");
        });

        // /api/embed
        builder.MapPost("/api/embed", async ([FromBody] JsonElement body, HttpContext ctx, CancellationToken ct) =>
        {
            var reqId = Guid.NewGuid().ToString("N")[..8];
            ctx.Response.Headers.Append("X-Request-Id", reqId);
            var model = body.GetProperty("model").GetString() ?? "";

            if (!router.IsManaged(model))
                return await ProxyToNative(httpFactory, bridgeConfig, body, "/api/embed", ct);

            if (!router.SupportsEmbed(model))
                return Results.BadRequest(new { error = "Modele non supporte pour embed" });

            var providerId = router.ResolveProvider(model)!;
            var modelEntry = modelsConfig[model];

            var started = await launcher.EnsureRunningAsync(providerId, model, ct);
            if (!started) return Results.StatusCode(503);

            var providerUrl = launcher.GetProviderUrl(providerId);
            var providerType = providersConfig[providerId].Type;
            var embedBody = await adapter.ToProviderFormatAsync(body, modelEntry.ModelPath, providerType);

            var client = httpFactory.CreateClient("backend");
            var sw = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync($"{providerUrl}/v1/embeddings", embedBody, ct);
            sw.Stop();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var embeddings = result.GetProperty("data").EnumerateArray()
                .Select(d => d.GetProperty("embedding").EnumerateArray().Select(x => x.GetDouble()).ToArray())
                .ToList();

            var proc = launcher.GetProcess(providerId);
            var ramMb = proc != null ? proc.WorkingSet64 / (1024 * 1024) : 0;
            var device = modelEntry.Device ?? "AUTO";

            metrics.Record(reqId, model, providerId, device, sw.Elapsed.TotalMilliseconds, 0, ramMb, sw.ElapsedMilliseconds);
            logSink.Write("INF", $"[req-{reqId}] {model} embed | {sw.ElapsedMilliseconds}ms");

            return Results.Json(new { model, embeddings, total_duration = sw.ElapsedMilliseconds });
        });

        // /v1/chat/completions
        builder.MapPost("/v1/chat/completions", async ([FromBody] JsonElement body, HttpContext ctx, CancellationToken ct) =>
        {
            var reqId = Guid.NewGuid().ToString("N")[..8];
            ctx.Response.Headers.Append("X-Request-Id", reqId);
            var model = body.GetProperty("model").GetString() ?? "";

            if (!router.IsManaged(model))
                return await ProxyToNative(httpFactory, bridgeConfig, body, "/v1/chat/completions", ct);

            var providerId = router.ResolveProvider(model)!;
            var modelEntry = modelsConfig[model];

            var started = await launcher.EnsureRunningAsync(providerId, model, ct);
            if (!started) return Results.StatusCode(503);

            var providerUrl = launcher.GetProviderUrl(providerId);
            var client = httpFactory.CreateClient("backend");
            var response = await client.PostAsJsonAsync($"{providerUrl}/v1/chat/completions", body, ct);

            return Results.Stream(await response.Content.ReadAsStreamAsync(ct), response.Content.Headers.ContentType?.ToString() ?? "application/json");
        });

        // /api/tags
        builder.MapGet("/api/tags", async (CancellationToken ct) =>
        {
            var client = httpFactory.CreateClient();
            var native = await client.GetFromJsonAsync<JsonElement>($"{bridgeConfig.OllamaNative}/api/tags", ct);
            var models = native.TryGetProperty("models", out var m) ? m.Deserialize<List<JsonElement>>() ?? new() : new List<JsonElement>();

            foreach (var (name, cfg) in modelsConfig)
            {
                var provider = providersConfig[cfg.ProviderId];
                models.Add(JsonSerializer.SerializeToElement(new
                {
                    name,
                    model = name,
                    modified_at = DateTime.UtcNow.ToString("o"),
                    size = 0L,
                    digest = $"ov-{name}-{cfg.ProviderId}",
                    details = new { family = cfg.ProviderId, parameter_size = cfg.ContextSize?.ToString() ?? "?" }
                }));
            }
            return Results.Json(new { models });
        });

        // /api/version
        builder.MapGet("/api/version", () => Results.Json(new { version = "0.5.0-ollamock" }));

        // /v1/models
        builder.MapGet("/v1/models", () =>
        {
            var models = modelsConfig.Select(m => new
            {
                id = m.Key,
                @object = "model",
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                owned_by = m.Value.ProviderId
            }).ToList();
            return Results.Json(new { @object = "list", data = models });
        });

        return builder.Build();
    }

    private static async Task<IResult> ProxyToNative(IHttpClientFactory factory, BridgeConfig config, JsonElement body, string path, CancellationToken ct)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync($"{config.OllamaNative}{path}", body, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(content, response.Content.Headers.ContentType?.ToString(), statusCode: (int)response.StatusCode);
    }
}

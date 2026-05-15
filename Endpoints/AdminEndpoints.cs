using OllamaBridge.Config;
using OllamaBridge.Core;
using OllamaBridge.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using System.Text.Json;

namespace OllamaBridge.Endpoints;

public static class AdminRouteExtensions
{
    public static IApplicationBuilder MapAdminRoutes(this IApplicationBuilder app)
    {
        var router = app.ApplicationServices.GetRequiredService<IRouter>();
        var launcher = app.ApplicationServices.GetRequiredService<IBackendLauncher>();
        var launcherImpl = app.ApplicationServices.GetRequiredService<IBackendLauncher>() as BackendLauncher;
        var httpFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();
        var logSink = app.ApplicationServices.GetRequiredService<LogSink>();
        var metrics = app.ApplicationServices.GetRequiredService<MetricsSink>();
        var config = app.ApplicationServices.GetRequiredService<IOptions<AppSettings>>().Value;

        var builder = app.New();

        // ============ Status ============
        builder.MapGet("/admin/status", () =>
        {
            var ramUsed = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            var backendStatuses = config.Backends.Select(b => new
            {
                id = b.Key,
                port = b.Value.Port,
                running = launcherImpl?.GetProcess(b.Key) != null,
                healthy = false // async non supporte ici, client le re-poll
            }).ToList();

            return Results.Json(new
            {
                bridge = "online",
                ollamaNative = config.Bridge.OllamaNative,
                ramUsed,
                backends = backendStatuses,
                timestamp = DateTime.UtcNow
            });
        });

        // ============ Models ============
        builder.MapGet("/admin/models", () =>
        {
            var result = config.Models.Select(m =>
            {
                var proc = launcherImpl?.GetProcess(m.Value.BackendId);
                var startTime = launcherImpl?.GetStartTime(m.Value.BackendId);
                return new
                {
                    name = m.Key,
                    backend = m.Value.BackendId,
                    device = m.Value.Device ?? "AUTO",
                    fallbackDevice = m.Value.FallbackDevice ?? "CPU",
                    path = m.Value.ModelPath,
                    contextSize = m.Value.ContextSize,
                    status = proc != null ? "running" : "stopped",
                    loaded = proc != null,
                    pid = proc?.Id,
                    uptime = startTime != null ? (DateTime.UtcNow - startTime.Value).ToString(@"hh\:mm\:ss") : null,
                    lastUsed = startTime != null ? startTime.Value.ToString("O") : null,
                    ramUsed = proc != null ? proc.WorkingSet64 / (1024 * 1024) : 0
                };
            });
            return Results.Json(result);
        });

        builder.MapPost("/admin/models/{id}/test", async (string id, CancellationToken ct) =>
        {
            if (!config.Models.TryGetValue(id, out var model)) return Results.NotFound();
            if (!router.SupportsGenerate(id)) return Results.BadRequest("Not a generate model");

            var backendId = model.BackendId;
            var started = await launcher.EnsureRunningAsync(backendId, id, ct);
            if (!started) return Results.StatusCode(503);

            var backendUrl = launcher.GetBackendUrl(backendId);
            var client = httpFactory.CreateClient("backend");

            var sw = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync($"{backendUrl}/v1/chat/completions", new
            {
                model = model.ModelPath,
                messages = new[] { new { role = "user", content = "Hello, respond with one word." } },
                stream = false,
                max_tokens = 10
            }, ct);
            sw.Stop();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var content = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            var tokens = result.GetProperty("usage").GetProperty("total_tokens").GetInt32();
            var tokS = tokens / (sw.ElapsedMilliseconds / 1000.0);

            return Results.Json(new
            {
                model = id,
                response = content,
                ttft = sw.ElapsedMilliseconds,
                tokS,
                tokens,
                ramUsed = launcherImpl?.GetProcess(backendId)?.WorkingSet64 / (1024 * 1024) ?? 0
            });
        });

        builder.MapPost("/admin/models/{id}/toggle", async (string id, CancellationToken ct) =>
        {
            if (!config.Models.TryGetValue(id, out var model)) return Results.NotFound();
            var backendId = model.BackendId;
            var proc = launcherImpl?.GetProcess(backendId);

            if (proc != null)
            {
                await launcher.StopAsync(backendId);
                return Results.Json(new { action = "stopped", backend = backendId });
            }
            else
            {
                var started = await launcher.EnsureRunningAsync(backendId, id, ct);
                return started
                    ? Results.Json(new { action = "started", backend = backendId })
                    : Results.StatusCode(503);
            }
        });

        // ============ Backends ============
        builder.MapGet("/admin/backends", () =>
        {
            var result = config.Backends.Select(b =>
            {
                var proc = launcherImpl?.GetProcess(b.Key);
                return new
                {
                    id = b.Key,
                    port = b.Value.Port,
                    executable = b.Value.Executable,
                    running = proc != null,
                    healthy = false, // polled client-side
                    restartCount = launcherImpl?.GetRestartCount(b.Key) ?? 0,
                    runtimeVersion = b.Value.RuntimeVersion
                };
            });
            return Results.Json(result);
        });

        builder.MapPost("/admin/backends/{id}/start", async (string id, CancellationToken ct) =>
        {
            if (!config.Backends.ContainsKey(id)) return Results.NotFound();
            var model = config.Models.FirstOrDefault(m => m.Value.BackendId == id).Key;
            if (model == null) return Results.BadRequest("No model mapped to this backend");
            var started = await launcher.EnsureRunningAsync(id, model, ct);
            return started ? Results.Ok() : Results.StatusCode(503);
        });

        builder.MapPost("/admin/backends/{id}/stop", async (string id) =>
        {
            if (!config.Backends.ContainsKey(id)) return Results.NotFound();
            await launcher.StopAsync(id);
            return Results.Ok();
        });

        builder.MapPost("/admin/backends/{id}/restart", async (string id, CancellationToken ct) =>
        {
            if (!config.Backends.ContainsKey(id)) return Results.NotFound();
            await launcher.StopAsync(id);
            var model = config.Models.FirstOrDefault(m => m.Value.BackendId == id).Key;
            if (model == null) return Results.BadRequest("No model mapped");
            var started = await launcher.EnsureRunningAsync(id, model, ct);
            return started ? Results.Ok() : Results.StatusCode(503);
        });

        // ============ Logs ============
        builder.MapGet("/admin/logs", (int? count) => Results.Json(logSink.GetRecent(count ?? 100)));

        builder.MapGet("/admin/logs/file", (string? date) =>
        {
            var path = logSink.GetLogFilePath(date);
            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(path, "text/plain", Path.GetFileName(path));
        });

        // ============ Metrics ============
        builder.MapGet("/admin/metrics", (int? count) => Results.Json(metrics.GetRecent(count ?? 50)));

        builder.MapGet("/admin/metrics/summary", () =>
        {
            var lines = metrics.GetRecent(1000);
            var parsed = lines.Select(l => l.Split(',')).Where(p => p.Length >= 8).ToList();
            var summary = parsed.GroupBy(p => p[2]) // model
                .Select(g => new
                {
                    model = g.Key,
                    count = g.Count(),
                    avgTtft = g.Average(x => double.TryParse(x[5], out var v) ? v : 0),
                    avgTokS = g.Average(x => double.TryParse(x[6], out var v) ? v : 0),
                    avgRam = g.Average(x => long.TryParse(x[7], out var v) ? v : 0)
                });
            return Results.Json(summary);
        });

        // ============ Config ============
        builder.MapGet("/admin/config", () =>
        {
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
            return Results.Content(json, "application/json");
        });

        builder.MapPost("/admin/config", async (HttpRequest req) =>
        {
            var json = await req.ReadAsStringAsync();
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            await File.WriteAllTextAsync(path, json);
            return Results.Ok();
        });

        builder.MapPost("/admin/config/validate", async (HttpRequest req) =>
        {
            try
            {
                var json = await req.ReadAsStringAsync();
                JsonSerializer.Deserialize<AppSettings>(json);
                return Results.Ok(new { valid = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { valid = false, error = ex.Message });
            }
        });

        builder.MapPost("/admin/config/reload", () =>
        {
            // Le reloadOnChange: true s'en charge
            return Results.Ok(new { reloaded = true });
        });

        return builder.Build();
    }
}

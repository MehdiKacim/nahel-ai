using System.Diagnostics;
using System.Text.Json;

namespace Ollamock.Service.Server;

public static class AdminRouteExtensions
{
    public static IApplicationBuilder MapAdminRoutes(this IApplicationBuilder app)
    {
        var router = app.ApplicationServices.GetRequiredService<IRouter>();
        var launcher = app.ApplicationServices.GetRequiredService<IBackendLauncher>();
        var httpFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();
        var logSink = app.ApplicationServices.GetRequiredService<LogSink>();
        var metrics = app.ApplicationServices.GetRequiredService<MetricsSink>();
        var bridgeConfig = app.ApplicationServices.GetRequiredService<IOptions<BridgeConfig>>().Value;
        var modelsConfig = app.ApplicationServices.GetRequiredService<IOptions<ModelsConfig>>().Value;
        var providersConfig = app.ApplicationServices.GetRequiredService<IOptions<ProvidersConfig>>().Value;
        var launcherRegistry = app.ApplicationServices.GetRequiredService<ILauncherRegistry>();

        var builder = app.New();

        // Status
        builder.MapGet("/admin/status", () =>
        {
            var ramUsed = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            var backendStatuses = providersConfig.Select(p => new
            {
                id = p.Key,
                port = p.Value.Port,
                type = p.Value.Type,
                running = launcher.GetProcess(p.Key) != null,
                restartCount = launcher.GetRestartCount(p.Key),
                runtimeVersion = p.Value.RuntimeVersion
            }).ToList();

            return Results.Json(new
            {
                bridge = "online",
                ollamaNative = bridgeConfig.OllamaNative,
                ramUsed,
                backends = backendStatuses,
                timestamp = DateTime.UtcNow
            });
        });

        // Models
        builder.MapGet("/admin/models", () =>
        {
            var result = modelsConfig.Select(m =>
            {
                var proc = launcher.GetProcess(m.Value.ProviderId);
                var startTime = launcher.GetStartTime(m.Value.ProviderId);
                return new
                {
                    name = m.Key,
                    provider = m.Value.ProviderId,
                    device = m.Value.Device ?? "AUTO",
                    fallbackDevice = m.Value.FallbackDevice ?? "CPU",
                    path = m.Value.ModelPath,
                    contextSize = m.Value.ContextSize,
                    status = proc != null ? "running" : "stopped",
                    loaded = proc != null,
                    pid = proc?.Id,
                    uptime = startTime != null ? (DateTime.UtcNow - startTime.Value).ToString("hh\:mm\:ss") : null,
                    lastUsed = startTime?.ToString("O"),
                    ramUsed = proc != null ? proc.WorkingSet64 / (1024 * 1024) : 0
                };
            });
            return Results.Json(result);
        });

        builder.MapPost("/admin/models/{id}/test", async (string id, CancellationToken ct) =>
        {
            if (!modelsConfig.TryGetValue(id, out var model)) return Results.NotFound();
            if (!router.SupportsGenerate(id)) return Results.BadRequest("Not a generate model");

            var providerId = model.ProviderId;
            var started = await launcher.EnsureRunningAsync(providerId, id, ct);
            if (!started) return Results.StatusCode(503);

            var providerUrl = launcher.GetProviderUrl(providerId);
            var client = httpFactory.CreateClient("backend");

            var sw = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync($"{providerUrl}/v1/chat/completions", new
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
                ramUsed = launcher.GetProcess(providerId)?.WorkingSet64 / (1024 * 1024) ?? 0
            });
        });

        builder.MapPost("/admin/models/{id}/toggle", async (string id, CancellationToken ct) =>
        {
            if (!modelsConfig.TryGetValue(id, out var model)) return Results.NotFound();
            var providerId = model.ProviderId;
            var proc = launcher.GetProcess(providerId);

            if (proc != null)
            {
                await launcher.StopAsync(providerId);
                return Results.Json(new { action = "stopped", provider = providerId });
            }
            else
            {
                var started = await launcher.EnsureRunningAsync(providerId, id, ct);
                return started
                    ? Results.Json(new { action = "started", provider = providerId })
                    : Results.StatusCode(503);
            }
        });

        // Backends
        builder.MapGet("/admin/backends", () =>
        {
            var result = providersConfig.Select(p =>
            {
                var proc = launcher.GetProcess(p.Key);
                return new
                {
                    id = p.Key,
                    port = p.Value.Port,
                    type = p.Value.Type,
                    executable = p.Value.Executable,
                    running = proc != null,
                    restartCount = launcher.GetRestartCount(p.Key),
                    runtimeVersion = p.Value.RuntimeVersion
                };
            });
            return Results.Json(result);
        });

        builder.MapPost("/admin/backends/{id}/start", async (string id, CancellationToken ct) =>
        {
            if (!providersConfig.ContainsKey(id)) return Results.NotFound();
            var model = modelsConfig.FirstOrDefault(m => m.Value.ProviderId == id).Key;
            if (model == null) return Results.BadRequest("No model mapped");
            var started = await launcher.EnsureRunningAsync(id, model, ct);
            return started ? Results.Ok() : Results.StatusCode(503);
        });

        builder.MapPost("/admin/backends/{id}/stop", async (string id) =>
        {
            if (!providersConfig.ContainsKey(id)) return Results.NotFound();
            await launcher.StopAsync(id);
            return Results.Ok();
        });

        builder.MapPost("/admin/backends/{id}/restart", async (string id, CancellationToken ct) =>
        {
            if (!providersConfig.ContainsKey(id)) return Results.NotFound();
            await launcher.StopAsync(id);
            var model = modelsConfig.FirstOrDefault(m => m.Value.ProviderId == id).Key;
            if (model == null) return Results.BadRequest("No model mapped");
            var started = await launcher.EnsureRunningAsync(id, model, ct);
            return started ? Results.Ok() : Results.StatusCode(503);
        });

        // Launchers - NEW
        builder.MapGet("/admin/launchers", async () =>
        {
            var results = new List<object>();
            foreach (var launcher in launcherRegistry.GetAll())
            {
                var detect = await launcher.DetectAsync();
                var running = await launcher.IsRunningAsync();
                var configured = await launcher.IsConfiguredAsync();
                var canInstall = await launcher.IsInstallSupportedAsync();

                results.Add(new
                {
                    name = launcher.Name,
                    displayName = launcher.DisplayName,
                    description = launcher.Description,
                    apiFormat = launcher.ApiFormat,
                    category = launcher.Category,
                    installed = detect.Installed,
                    version = detect.Version,
                    path = detect.Path,
                    running,
                    configured,
                    canInstall,
                    installCommand = launcher.GetInstallCommand(),
                    installHint = detect.InstallHint,
                    homepageUrl = launcher.GetHomepageUrl(),
                    documentationUrl = launcher.GetDocumentationUrl()
                });
            }
            return Results.Json(results);
        });

        builder.MapPost("/admin/launchers/{name}/detect", async (string name) =>
        {
            var launcher = launcherRegistry.Get(name);
            if (launcher == null) return Results.NotFound();
            var result = await launcher.DetectAsync();
            return Results.Json(result);
        });

        builder.MapPost("/admin/launchers/{name}/install", async (string name) =>
        {
            var launcher = launcherRegistry.Get(name);
            if (launcher == null) return Results.NotFound();
            if (!await launcher.IsInstallSupportedAsync())
                return Results.BadRequest(new { error = "Auto-install not supported for this tool" });

            var result = await launcher.InstallAsync();
            return result.Success 
                ? Results.Ok(new { success = true, output = result.Output })
                : Results.BadRequest(new { success = false, error = result.Error });
        });

        builder.MapPost("/admin/launchers/{name}/configure", async (string name, [FromBody] JsonElement body) =>
        {
            var launcher = launcherRegistry.Get(name);
            if (launcher == null) return Results.NotFound();
            var bridgeUrl = body.GetProperty("bridgeUrl").GetString() ?? $"http://localhost:{bridgeConfig.Listen.Split(':').Last()}";
            var model = body.TryGetProperty("model", out var m) ? m.GetString() : null;
            await launcher.ConfigureAsync(bridgeUrl, model);
            return Results.Ok(new { configured = true });
        });

        builder.MapPost("/admin/launchers/{name}/launch", async (string name, [FromBody] JsonElement? body) =>
        {
            var launcher = launcherRegistry.Get(name);
            if (launcher == null) return Results.NotFound();
            var model = body?.TryGetProperty("model", out var m) == true ? m.GetString() : null;
            await launcher.LaunchAsync(model);
            return Results.Ok(new { launched = true });
        });

        builder.MapPost("/admin/launchers/{name}/stop", async (string name) =>
        {
            var launcher = launcherRegistry.Get(name);
            if (launcher == null) return Results.NotFound();
            await launcher.StopAsync();
            return Results.Ok(new { stopped = true });
        });

        builder.MapPost("/admin/launchers/{name}/restore", async (string name) =>
        {
            var launcher = launcherRegistry.Get(name);
            if (launcher == null) return Results.NotFound();
            await launcher.RestoreAsync();
            return Results.Ok(new { restored = true });
        });

        // Logs
        builder.MapGet("/admin/logs", (int? count) => Results.Json(logSink.GetRecent(count ?? 100)));
        builder.MapGet("/admin/logs/file", (string? date) =>
        {
            var path = logSink.GetLogFilePath(date);
            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(path, "text/plain", Path.GetFileName(path));
        });

        // Metrics
        builder.MapGet("/admin/metrics", (int? count) => Results.Json(metrics.GetRecent(count ?? 50)));
        builder.MapGet("/admin/metrics/summary", () =>
        {
            var lines = metrics.GetRecent(1000);
            var parsed = lines.Select(l => l.Split(',')).Where(p => p.Length >= 8).ToList();
            var summary = parsed.GroupBy(p => p[2])
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

        // Config
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
                JsonSerializer.Deserialize<JsonElement>(json);
                return Results.Ok(new { valid = true });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { valid = false, error = ex.Message });
            }
        });

        builder.MapPost("/admin/config/reload", () => Results.Ok(new { reloaded = true }));

        return builder.Build();
    }
}

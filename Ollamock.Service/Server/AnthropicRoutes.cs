using System.Diagnostics;
using System.Text.Json;

namespace Ollamock.Service.Server;

public static class AnthropicRouteExtensions
{
    public static IApplicationBuilder MapAnthropicRoutes(this IApplicationBuilder app)
    {
        var router = app.ApplicationServices.GetRequiredService<IRouter>();
        var launcher = app.ApplicationServices.GetRequiredService<IBackendLauncher>();
        var openAiAdapter = app.ApplicationServices.GetRequiredService<IRequestAdapter>();
        var anthropicAdapter = app.ApplicationServices.GetRequiredService<IAnthropicAdapter>();
        var httpFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();
        var logSink = app.ApplicationServices.GetRequiredService<LogSink>();
        var metrics = app.ApplicationServices.GetRequiredService<MetricsSink>();
        var bridgeConfig = app.ApplicationServices.GetRequiredService<IOptions<BridgeConfig>>().Value;
        var modelsConfig = app.ApplicationServices.GetRequiredService<IOptions<ModelsConfig>>().Value;
        var providersConfig = app.ApplicationServices.GetRequiredService<IOptions<ProvidersConfig>>().Value;

        var builder = app.New();

        // POST /v1/messages (Anthropic format)
        builder.MapPost("/v1/messages", async ([FromBody] JsonElement body, HttpContext ctx, CancellationToken ct) =>
        {
            var reqId = Guid.NewGuid().ToString("N")[..8];
            ctx.Response.Headers.Append("X-Request-Id", reqId);

            var model = body.GetProperty("model").GetString() ?? "";

            if (!router.IsManaged(model))
            {
                // Fallback to native Ollama - convert Anthropic -> OpenAI -> proxy
                var openAiBody = await anthropicAdapter.ToOpenAiFormatAsync(body, model);
                var client = httpFactory.CreateClient();
                var response = await client.PostAsJsonAsync($"{bridgeConfig.OllamaNative}/v1/chat/completions", openAiBody, ct);
                var openAiResponse = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var anthropicResponse = await anthropicAdapter.FromOpenAiFormatAsync(openAiResponse, model);
                return Results.Json(anthropicResponse);
            }

            if (!router.SupportsGenerate(model))
                return Results.BadRequest(new { error = "Modele non supporte pour generate" });

            var providerId = router.ResolveProvider(model)!;
            var modelEntry = modelsConfig[model];

            var started = await launcher.EnsureRunningAsync(providerId, model, ct);
            if (!started) return Results.StatusCode(503);

            var providerUrl = launcher.GetProviderUrl(providerId);
            var providerType = providersConfig[providerId].Type;

            // Convert Anthropic -> OpenAI
            var openAiRequest = await anthropicAdapter.ToOpenAiFormatAsync(body, modelEntry.ModelPath);

            var client = httpFactory.CreateClient("backend");
            var sw = Stopwatch.StartNew();
            var response = await client.PostAsJsonAsync($"{providerUrl}/v1/chat/completions", openAiRequest, ct);
            var responseStream = await response.Content.ReadAsStreamAsync(ct);

            var proc = launcher.GetProcess(providerId);
            var ramMb = proc != null ? proc.WorkingSet64 / (1024 * 1024) : 0;
            var device = modelEntry.Device ?? "AUTO";

            return Results.Stream(async stream =>
            {
                await using var writer = new StreamWriter(stream);
                var fullContent = "";
                var tokenCount = 0;
                var firstToken = true;
                var ttft = 0.0;

                await foreach (var chunk in openAiAdapter.FromProviderStreamAsync(responseStream, model, ct))
                {
                    try
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(chunk);
                        if (data.TryGetProperty("response", out var resp))
                        {
                            var content = resp.GetString() ?? "";
                            if (firstToken && !string.IsNullOrEmpty(content))
                            {
                                ttft = sw.Elapsed.TotalMilliseconds;
                                firstToken = false;
                            }
                            fullContent += content;
                            tokenCount++;

                            // Stream partial Anthropic format
                            var partial = new
                            {
                                type = "content_block_delta",
                                index = 0,
                                delta = new { type = "text", text = content }
                            };
                            await writer.WriteLineAsync($"data: {JsonSerializer.Serialize(partial)}");
                            await writer.FlushAsync(ct);
                        }

                        if (data.TryGetProperty("done", out var done) && done.GetBoolean())
                        {
                            // Final message
                            var final = await anthropicAdapter.FromOpenAiFormatAsync(
                                JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new
                                {
                                    choices = new[] { new { message = new { content = fullContent } } },
                                    usage = new { prompt_tokens = tokenCount / 2, completion_tokens = tokenCount / 2 }
                                })), model);
                            await writer.WriteLineAsync($"data: {JsonSerializer.Serialize(final)}");
                        }
                    }
                    catch { }
                }

                sw.Stop();
                var tokS = tokenCount > 0 ? tokenCount / (sw.ElapsedMilliseconds / 1000.0) : 0;
                metrics.Record(reqId, model, providerId, device, ttft, tokS, ramMb, sw.ElapsedMilliseconds);
                logSink.Write("INF", $"[req-{reqId}] {model} (Anthropic) | {tokenCount}tok | {tokS:F1}tok/s");
            }, "text/event-stream");
        });

        return builder.Build();
    }
}

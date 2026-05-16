using System.Text.Json;
using Nahel.SDK.Models;
using Nahel.SDK.Routes;
using Nahel.Server.Services;

namespace Nahel.Server.Routes;

public static class OpenAiRoutes
{
    public static IEndpointRouteBuilder MapOpenAiRoutes(this IEndpointRouteBuilder app)
    {
        app.MapGet(NahelRoutes.OpenAiModels, (IModelRouter router) =>
            Results.Ok(new OpenAiModelListResponse("list", router.ListModels()
                .Select(m => new OpenAiModelInfo(m.ModelId, "model", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), m.EngineId))
                .ToList())));

        app.MapPost(NahelRoutes.OpenAiChatCompletions, async (OpenAiChatCompletionRequest request, IOpenAiRouter router, CancellationToken ct) =>
        {
            if (request.Stream)
            {
                return Results.Stream(async stream =>
                {
                    await using var writer = new StreamWriter(stream);
                    await foreach (var chunk in router.RouteStreamChatCompletionAsync(request, ct))
                    {
                        await writer.WriteLineAsync($"data: {JsonSerializer.Serialize(chunk)}");
                        await writer.FlushAsync();
                    }
                    await writer.WriteLineAsync("data: [DONE]");
                }, "text/event-stream");
            }
            return Results.Ok(await router.RouteChatCompletionAsync(request, ct));
        });

        return app;
    }
}

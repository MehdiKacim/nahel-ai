using System.Text.Json;
using Nahel.Server.Services;

namespace Nahel.Server.Routes;

public static class OllamaCompatibilityRoutes
{
    public static IEndpointRouteBuilder MapOllamaRoutes(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/generate", (JsonElement body) =>
        {
            var model = body.GetProperty("model").GetString() ?? "";
            return Results.Ok(new { model, response = "", done = true });
        });

        app.MapGet("/api/tags", (IModelRouter router) =>
            Results.Ok(new { models = router.ListModels().Select(m => new { name = m.ModelId, model = m.ModelId }) }));

        app.MapGet("/api/version", () => Results.Ok(new { version = "0.1.0-nahel" }));

        return app;
    }
}

namespace Nahel.Server.Dashboard;

public sealed class DashboardMiddleware
{
    private readonly RequestDelegate _next;

    public DashboardMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/dashboard", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(Html);
            return;
        }

        await _next(context);
    }

    private const string Html = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>Nahel Dashboard</title>
<style>body{font-family:system-ui;margin:2rem;}h1{color:#2563eb;}section{margin:1rem 0;padding:1rem;border:1px solid #e5e7eb;border-radius:.5rem;}</style>
</head><body>
<h1>Nahel Dashboard</h1>
<section><h2>Status</h2><p>Server is running.</p></section>
<section><h2>Engines</h2><p>See API /engine</p></section>
<section><h2>Models</h2><p>See API /v1/models</p></section>
</body></html>";
}

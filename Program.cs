using OllamaBridge.Config;
using OllamaBridge.Core;
using OllamaBridge.Services;
using OllamaBridge.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "OLLAMA_BRIDGE_");

builder.WebHost.UseUrls(builder.Configuration.GetSection("Bridge:Listen").Value ?? "http://localhost:11434");

builder.Services.Configure<AppSettings>(builder.Configuration);
builder.Services.AddSingleton<IBackendLauncher, BackendLauncher>();
builder.Services.AddSingleton<IRouter, ConfigRouter>();
builder.Services.AddSingleton<IRequestAdapter, OpenAiAdapter>();
builder.Services.AddSingleton<LogSink>();
builder.Services.AddSingleton<MetricsSink>();
builder.Services.AddHttpClient("backend", c => c.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddHostedService<BridgeLifecycleService>();
builder.Services.AddHostedService<ProcessWatchdog>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();
app.UseStaticFiles();
app.MapFallbackToFile("admin/index.html");
app.MapOllamaRoutes();
app.MapAdminRoutes();
app.Run();

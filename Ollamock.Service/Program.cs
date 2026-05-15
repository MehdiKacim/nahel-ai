using Ollamock.Service.Bridge;
using Ollamock.Service.Launchers;
using Ollamock.Service.Providers;
using Ollamock.Service.Server;
using Ollamock.Service.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables(prefix: "OLLAMOCK_");

builder.WebHost.UseUrls(builder.Configuration.GetSection("Bridge:Listen").Value ?? "http://localhost:11434");

builder.Services.Configure<BridgeConfig>(builder.Configuration.GetSection("Bridge"));
builder.Services.Configure<ProvidersConfig>(builder.Configuration.GetSection("Providers"));
builder.Services.Configure<ModelsConfig>(builder.Configuration.GetSection("Models"));

builder.Services.AddSingleton<IBackendLauncher, BackendLauncher>();
builder.Services.AddSingleton<IRouter, ConfigRouter>();
builder.Services.AddSingleton<IRequestAdapter, OpenAiAdapter>();
builder.Services.AddSingleton<IAnthropicAdapter, AnthropicAdapter>();
builder.Services.AddSingleton<LogSink>();
builder.Services.AddSingleton<MetricsSink>();
builder.Services.AddSingleton<ILauncherRegistry, LauncherRegistry>();
builder.Services.AddSingleton<IConfigBackup, ConfigBackup>();

builder.Services.AddHttpClient("backend", c => c.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddHostedService<BridgeLifecycleService>();
builder.Services.AddHostedService<ProcessWatchdog>();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();
app.MapOllamaRoutes();
app.MapAnthropicRoutes();
app.MapAdminRoutes();
app.Run();

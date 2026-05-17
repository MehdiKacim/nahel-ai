using Nahel.Engine.OVGenAI;
using Nahel.Engine.Ovms;
using Nahel.SDK.Abstractions;
using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class NahelBootstrapService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NahelBootstrapService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public NahelBootstrapService(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger<NahelBootstrapService> logger,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Nahel bootstrap starting...");

        var catalog = _services.GetRequiredService<IBackendCatalog>();
        var modelRouter = _services.GetRequiredService<IModelRouter>();
        var runtime = _services.GetRequiredService<IBackendRuntime>();

        var backendsSection = _configuration.GetSection("backends");
        var modelsSection = _configuration.GetSection("models");

        if (backendsSection.GetChildren().Any())
        {
            await BootstrapDeclarativeAsync(backendsSection, modelsSection, catalog, modelRouter, ct);
        }
        else
        {
            await BootstrapLegacyAsync(modelsSection, catalog, modelRouter, ct);
        }

        _logger.LogInformation("Nahel bootstrap complete.");
    }

    private async Task BootstrapDeclarativeAsync(
        IConfigurationSection backendsSection,
        IConfigurationSection modelsSection,
        IBackendCatalog catalog,
        IModelRouter modelRouter,
        CancellationToken ct)
    {
        _logger.LogInformation("Using declarative backend configuration.");
        var modelEntries = modelsSection.GetChildren().ToList();
        int portOffset = 0;

        foreach (var beEntry in backendsSection.GetChildren())
        {
            var backendId = beEntry.Key;
            var backendType = beEntry.GetValue<string>("type") ?? "ovgenai";
            var device = beEntry.GetValue<string>("device") ?? "CPU";

            var linkedModel = modelEntries.FirstOrDefault(m =>
                m.GetValue<string>("backend") == backendId);

            if (linkedModel == null)
            {
                _logger.LogWarning("Backend '{BackendId}' has no linked model. Skipping.", backendId);
                continue;
            }

            var modelPath = linkedModel.GetValue<string>("path") ?? "";
            var modelName = linkedModel.GetValue<string>("name") ?? linkedModel.Key;
            var resolvedModelPath = ResolveRelativePath(modelPath);
            var modelExists = Directory.Exists(resolvedModelPath) && Directory.EnumerateFileSystemEntries(resolvedModelPath).Any();
            var repoId = linkedModel.GetValue<string>("repo_id") ?? "";

            if (!modelExists)
            {
                if (!string.IsNullOrWhiteSpace(repoId))
                {
                    _logger.LogWarning("Model for backend '{BackendId}' not found at '{ModelPath}'. Queuing download...", backendId, resolvedModelPath);
                    var queue = _services.GetRequiredService<IBackendCommandQueue>();
                    await queue.EnqueueAsync(new JobRequest(
                        JobType.DownloadModel,
                        null,
                        linkedModel.Key,
                        new Dictionary<string, string>
                        {
                            ["repo_id"] = repoId,
                            ["local_dir"] = resolvedModelPath
                        }), ct);
                }
                else
                {
                    _logger.LogWarning("Model for backend '{BackendId}' not found. Run 'nahel models add'.", backendId);
                }
            }

            var backend = CreateBackend(backendType, backendId, modelName, modelPath, device, portOffset);
            if (backend == null)
            {
                _logger.LogWarning("Unknown backend type '{BackendType}' for '{BackendId}'", backendType, backendId);
                continue;
            }

            catalog.RegisterBackend(backend);
            _logger.LogInformation("Registered backend '{BackendId}'", backendId);

            if (modelExists)
            {
                try
                {
                    _logger.LogInformation("Auto-starting backend '{BackendId}'...", backendId);
                    await backend.StartAsync(ct);
                    _logger.LogInformation("Backend '{BackendId}' started.", backendId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-start backend '{BackendId}'", backendId);
                }
            }
            else
            {
                _logger.LogWarning("Backend '{BackendId}' registered but not started because model is missing.", backendId);
            }

            portOffset++;
        }

        foreach (var modelEntry in modelEntries)
        {
            var modelId = modelEntry.Key;
            var backendId = modelEntry.GetValue<string>("backend") ?? "";
            var modelName = modelEntry.GetValue<string>("name") ?? modelId;
            if (string.IsNullOrEmpty(backendId))
            {
                _logger.LogWarning("Model '{ModelId}' has no backend reference.", modelId);
                continue;
            }
            modelRouter.Register(modelId, backendId, modelName);
            _logger.LogInformation("Registered model '{ModelId}' -> backend '{BackendId}'", modelId, backendId);
        }
    }

    private async Task BootstrapLegacyAsync(
        IConfigurationSection modelsSection,
        IBackendCatalog catalog,
        IModelRouter modelRouter,
        CancellationToken ct)
    {
        _logger.LogInformation("Using legacy model-centric configuration.");
        int portOffset = 0;

        foreach (var modelEntry in modelsSection.GetChildren())
        {
            var backendType = modelEntry.GetValue<string>("backend") ?? "ovgenai";
            var modelId = modelEntry.Key;
            var modelName = modelEntry.GetValue<string>("name") ?? modelId;
            var modelPath = modelEntry.GetValue<string>("path") ?? "";
            var device = modelEntry.GetValue<string>("device") ?? "CPU";
            var backendId = modelEntry.GetValue<string>("backend_id") ?? $"{backendType}-{modelId}";

            _logger.LogInformation("Configuring model '{ModelId}' with backend '{BackendType}'", modelId, backendType);

            var resolvedModelPath = ResolveRelativePath(modelPath);
            var modelExists = Directory.Exists(resolvedModelPath) && Directory.EnumerateFileSystemEntries(resolvedModelPath).Any();
            var repoId = modelEntry.GetValue<string>("repo_id") ?? "";
            if (!modelExists)
            {
                if (!string.IsNullOrWhiteSpace(repoId))
                {
                    _logger.LogWarning("Model '{ModelId}' not found at '{ModelPath}'. Queuing download from '{RepoId}'...", modelId, resolvedModelPath, repoId);
                    var queue = _services.GetRequiredService<IBackendCommandQueue>();
                    await queue.EnqueueAsync(new JobRequest(
                        JobType.DownloadModel,
                        null,
                        modelId,
                        new Dictionary<string, string>
                        {
                            ["repo_id"] = repoId,
                            ["local_dir"] = resolvedModelPath
                        }), ct);
                }
                else
                {
                    _logger.LogWarning("Model '{ModelId}' not found at '{ModelPath}'. Run 'nahel models add' to configure and download it.", modelId, resolvedModelPath);
                }
            }

            var backend = CreateBackend(backendType, backendId, modelName, modelPath, device, portOffset);
            if (backend == null)
            {
                _logger.LogWarning("Unknown backend type '{BackendType}' for model '{ModelId}'", backendType, modelId);
                continue;
            }

            catalog.RegisterBackend(backend);
            modelRouter.Register(modelId, backend.EngineId, modelName);
            _logger.LogInformation("Registered model '{ModelId}' -> backend '{BackendId}'", modelId, backend.EngineId);

            if (modelExists)
            {
                try
                {
                    _logger.LogInformation("Auto-starting backend '{BackendId}'...", backend.EngineId);
                    await backend.StartAsync(ct);
                    _logger.LogInformation("Backend '{BackendId}' started.", backend.EngineId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-start backend '{BackendId}'", backend.EngineId);
                }
            }
            else
            {
                _logger.LogWarning("Backend '{BackendId}' registered but not started because model '{ModelId}' is missing.", backend.EngineId, modelId);
            }

            portOffset++;
        }
    }

    private IBackend? CreateBackend(string backendType, string backendId, string modelName, string modelPath, string device, int portOffset)
    {
        if (backendType.Equals("ovgenai", StringComparison.OrdinalIgnoreCase))
        {
            var options = new OVGenAIOptions
            {
                EngineId = backendId,
                DisplayName = $"OVGenAI ({backendId})",
                ModelName = modelName,
                ModelPath = modelPath,
                Device = device,
                Port = 8100 + portOffset,
            };
            var supervisor = new OVGenAIProcessSupervisor(_loggerFactory.CreateLogger<OVGenAIProcessSupervisor>());
            var httpClient = _httpClientFactory.CreateClient();
            return new OVGenAIBackend(options, supervisor, httpClient, _loggerFactory.CreateLogger<OVGenAIBackend>());
        }
        else if (backendType.Equals("ovms", StringComparison.OrdinalIgnoreCase))
        {
            var options = new OvmsOptions
            {
                EngineId = backendId,
                DisplayName = $"OVMS ({backendId})",
                ExecutablePath = ResolveRelativePath("backends\\ovms\\bin\\ovms.exe"),
                WorkingDirectory = ResolveRelativePath("backends\\ovms\\bin"),
                ModelName = modelName,
                ModelPath = modelPath,
                Device = device,
                RestPort = 8000 + portOffset,
                GrpcPort = 9000 + portOffset,
            };
            var supervisor = new OvmsProcessSupervisor();
            var healthClient = new OvmsHealthClient(_httpClientFactory.CreateClient());
            return new OvmsEngine(options, supervisor, healthClient, _httpClientFactory.CreateClient(), _loggerFactory.CreateLogger<OvmsEngine>());
        }
        return null;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static string ResolveRelativePath(string relativePath)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(baseDir, relativePath));
        if (File.Exists(candidate) || Directory.Exists(candidate))
            return candidate;

        var devCandidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", relativePath));
        if (File.Exists(devCandidate) || Directory.Exists(devCandidate))
            return devCandidate;

        return candidate;
    }
}

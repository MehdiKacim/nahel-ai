using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nahel.Cli.Commands;

public sealed class ModelsCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        using var client = new NahelApiClient();

        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintModelsHelp();
            return 0;
        }

        if (args[0] == "list")
        {
            var doc = await client.GetAsync("/models");
            if (doc == null)
            {
                Console.WriteLine("Server not running. Start it with: nahel start");
                return 1;
            }

            if (doc.RootElement.TryGetProperty("models", out var models))
            {
                foreach (var m in models.EnumerateArray())
                {
                    var id = m.TryGetProperty("model_id", out var pId) ? pId.GetString() : "?";
                    var backend = m.TryGetProperty("backend_type", out var pBe) ? pBe.GetString() : "?";
                    var state = m.TryGetProperty("state", out var pState) ? pState.GetString() : "?";
                    Console.WriteLine($"  {id,-20} [{backend,-8}] {state}");
                }
            }
            else
            {
                Console.WriteLine("No models registered.");
            }
            return 0;
        }

        if (args[0] == "download" && args.Length >= 2)
        {
            var repoId = args[1];
            var modelId = args.Length >= 3 ? args[2] : repoId.Split('/').Last();
            var backend = args.Length >= 4 ? args[3] : "ovgenai";
            var device = args.Length >= 5 ? args[4] : "CPU";

            Console.WriteLine($"Queueing download of {repoId} as '{modelId}'...");
            var result = await client.PostAsync("/models/download", new
            {
                modelId,
                repoId,
                backend,
                device
            });

            if (result == null)
            {
                Console.WriteLine("Failed to queue download. Is the server running?");
                return 1;
            }

            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (args[0] == "switch" && args.Length >= 2)
        {
            var modelId = args[1];
            var backendId = args.Length >= 3 ? args[2] : $"ovgenai-{modelId}";

            Console.WriteLine($"Switching to model '{modelId}' on backend '{backendId}'...");
            var result = await client.PostAsync("/models/switch", new
            {
                modelId,
                backendId,
                targetEngineModelName = modelId
            });

            if (result == null)
            {
                Console.WriteLine("Failed to switch model. Is the server running?");
                return 1;
            }

            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (args[0] == "rm" && args.Length >= 2)
        {
            var modelId = args[1];
            Console.WriteLine($"Removing model '{modelId}'...");
            var result = await client.DeleteAsync($"/models/{modelId}");
            if (result == null)
            {
                Console.WriteLine("Failed to remove model. Is the server running?");
                return 1;
            }
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        if (args[0] == "add")
        {
            return await ExecuteAddAsync();
        }

        PrintModelsHelp();
        return 1;
    }

    private static void PrintModelsHelp()
    {
        Console.WriteLine("""
            nahel models — Manage models

            Usage:
              nahel models <subcommand> [options]

            Subcommands:
              list       List registered models
              add        Interactive model setup (download + configure)
              download   Queue a model download
              switch     Switch active model
              rm         Remove a model

            Examples:
              nahel models list
              nahel models add
              nahel models download OpenVINO/Mistral-7B-Instruct-v0.2-int4-ov
              nahel models switch <model>
              nahel models rm <model>
            """);
    }

    private async Task<int> ExecuteAddAsync()
    {
        Console.Write("Model ID (e.g. mistral-7b): ");
        var modelId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(modelId)) { Console.WriteLine("Model ID is required."); return 1; }

        Console.Write("HuggingFace repo ID (e.g. OpenVINO/Mistral-7B-Instruct-v0.2-int4-ov): ");
        var repoId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(repoId)) { Console.WriteLine("Repo ID is required."); return 1; }

        Console.Write("Backend (ovgenai/ovms) [ovgenai]: ");
        var backend = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(backend)) backend = "ovgenai";

        Console.Write("Backend ID (optional, e.g. ovms-main) [auto]: ");
        var backendId = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(backendId)) backendId = null;

        Console.Write("Device (CPU/GPU/NPU) [CPU]: ");
        var device = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(device)) device = "CPU";
        var validDevices = new[] { "CPU", "GPU", "NPU" };
        if (!validDevices.Contains(device, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Invalid device '{device}'. Must be CPU, GPU, or NPU.");
            return 1;
        }
        device = device.ToUpperInvariant();

        var localPath = Path.Combine("models", modelId);

        var fullLocalPath = Path.GetFullPath(localPath);
        if (!Directory.Exists(fullLocalPath) || !Directory.EnumerateFileSystemEntries(fullLocalPath).Any())
        {
            Console.WriteLine($"Model not found locally at {fullLocalPath}.");
            Console.Write("Download now via git lfs? (y/n) [y]: ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(answer)) answer = "y";
            if (answer == "y" || answer == "yes")
            {
                Console.WriteLine($"Downloading {repoId}...");
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"lfs clone https://huggingface.co/{repoId} \"{fullLocalPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) { Console.WriteLine("Failed to start git."); return 1; }
                await proc.WaitForExitAsync();
                if (proc.ExitCode != 0)
                {
                    var err = await proc.StandardError.ReadToEndAsync();
                    Console.WriteLine($"Download failed: {err}");
                    return 1;
                }
                Console.WriteLine("Download complete.");
            }
        }

        // Locate nahel.json
        var configPath = FindNahelJsonPath();
        if (configPath == null)
        {
            Console.WriteLine("nahel.json not found. Creating a new one.");
            configPath = Path.Combine(AppContext.BaseDirectory, "nahel.json");
            var defaultConfig = new Dictionary<string, object>
            {
                ["server"] = new Dictionary<string, object> { ["host"] = "127.0.0.1", ["port"] = 11435, ["apiKey"] = "local" },
                ["models"] = new Dictionary<string, object>()
            };
            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
        }

        var jsonText = await File.ReadAllTextAsync(configPath);
        var configNode = JsonNode.Parse(jsonText)?.AsObject() ?? new JsonObject();
        var modelsNode = configNode["models"]?.AsObject() ?? new JsonObject();
        var modelEntryDict = new Dictionary<string, string>
        {
            ["backend"] = backend,
            ["path"] = localPath,
            ["device"] = device,
            ["repo_id"] = repoId
        };
        if (!string.IsNullOrEmpty(backendId))
            modelEntryDict["backend_id"] = backendId;
        modelsNode[modelId] = JsonSerializer.SerializeToNode(modelEntryDict);
        configNode["models"] = modelsNode;
        await File.WriteAllTextAsync(configPath, configNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Model '{modelId}' added to {configPath}.");

        using var client = new NahelApiClient();
        var status = await client.GetAsync("/api/status");
        if (status != null)
        {
            Console.WriteLine("Server is running. Restart it to activate the new model: nahel stop && nahel start");
        }
        else
        {
            Console.WriteLine("Start the server when ready: nahel start");
        }

        return 0;
    }

    private static string? FindNahelJsonPath()
    {
        // 1. Next to the executable
        var candidate = Path.Combine(AppContext.BaseDirectory, "nahel.json");
        if (File.Exists(candidate)) return candidate;

        // 2. Walk up from current directory
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir != null)
        {
            var p = Path.Combine(dir.FullName, "nahel.json");
            if (File.Exists(p)) return p;
            dir = dir.Parent;
        }
        return null;
    }
}

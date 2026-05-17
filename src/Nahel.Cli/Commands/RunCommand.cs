using System.Text.Json;

namespace Nahel.Cli.Commands;

public sealed class RunCommand : ICommand
{
    public async Task<int> ExecuteAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: nahel run <model>");
            return 1;
        }

        var modelId = args[0];
        using var client = new NahelApiClient();

        // Verify server is up and locate the backend for this model
        var statusDoc = await client.GetAsync("/api/status");
        if (statusDoc == null)
        {
            Console.WriteLine("Nahel server is not running. Start it with: nahel start");
            return 1;
        }

        string? backendId = null;
        if (statusDoc.RootElement.TryGetProperty("models", out var modelsArr))
        {
            foreach (var m in modelsArr.EnumerateArray())
            {
                var id = m.TryGetProperty("model_id", out var pId) ? pId.GetString() : null;
                if (id == modelId)
                {
                    backendId = m.TryGetProperty("backend_id", out var pBe) ? pBe.GetString() : null;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(backendId))
        {
            Console.WriteLine($"Model '{modelId}' is not registered. Add it first with: nahel models add");
            return 1;
        }

        // Check backend state
        string backendState = "unknown";
        if (statusDoc.RootElement.TryGetProperty("backends", out var backendsArr))
        {
            foreach (var b in backendsArr.EnumerateArray())
            {
                var id = b.TryGetProperty("engine_id", out var pId) ? pId.GetString() : null;
                if (id == backendId)
                {
                    backendState = b.TryGetProperty("state", out var pState) ? pState.GetString() ?? "unknown" : "unknown";
                    break;
                }
            }
        }

        // Start backend if not running
        if (!string.Equals(backendState, "Running", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Backend '{backendId}' is {backendState}. Sending start request...");
            var startResult = await client.PostAsync($"/engine/{backendId}/start", new { });
            if (startResult == null)
            {
                Console.WriteLine($"Failed to send start request for backend '{backendId}'.");
                return 1;
            }

            string? jobId = null;
            if (startResult.RootElement.TryGetProperty("job_id", out var jobIdProp))
                jobId = jobIdProp.GetString();

            if (string.IsNullOrEmpty(jobId))
            {
                Console.WriteLine("Start request did not return a job ID. Falling back to legacy polling...");
                await WaitForBackendLegacy(client, backendId);
            }
            else
            {
                Console.Write($"Waiting for start job '{jobId}' to complete");
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(2000);
                    var jobDoc = await client.GetAsync($"/jobs/{jobId}");
                    if (jobDoc != null)
                    {
                        var jobStatusInt = jobDoc.RootElement.TryGetProperty("status", out var s) ? s.GetInt32() : -1;
                        if (jobStatusInt == 2)
                        {
                            Console.WriteLine(" OK");
                            break;
                        }
                        if (jobStatusInt == 3)
                        {
                            var err = jobDoc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : "Unknown error";
                            Console.WriteLine(" FAILED");
                            Console.WriteLine($"Backend start failed: {err}");
                            return 1;
                        }
                    }
                    Console.Write(".");
                    Console.Out.Flush();
                    if (i == 59)
                    {
                        Console.WriteLine(" timeout");
                        return 1;
                    }
                }
            }

            Console.WriteLine("Backend process started. It may take several minutes to load the model.");
            Console.Write("Waiting for backend to become healthy");
            for (int i = 0; i < 600; i++)
            {
                await Task.Delay(2000);
                var health = await client.GetAsync($"/engine/{backendId}/health");
                if (health != null)
                {
                    var reachable = health.RootElement.TryGetProperty("reachable", out var r) && r.GetBoolean();
                    var msg = health.RootElement.TryGetProperty("status_message", out var m) ? m.GetString() : "";
                    if (reachable)
                    {
                        Console.WriteLine(" OK");
                        break;
                    }
                    Console.Write($".({msg})");
                }
                else
                {
                    Console.Write(".");
                }
                Console.Out.Flush();
                if (i == 599)
                {
                    Console.WriteLine(" timeout");
                    Console.WriteLine("Backend did not become healthy within 20 minutes.");
                    return 1;
                }
            }
        }

        // Switch to the requested model (server resolves model path from config)
        var switchResult = await client.PostAsync("/models/switch", new
        {
            modelId,
            backendId,
            targetEngineModelName = (string?)null
        });

        if (switchResult == null)
        {
            Console.WriteLine($"Failed to load model '{modelId}'.");
            return 1;
        }

        if (switchResult.RootElement.TryGetProperty("success", out var successProp) && successProp.GetBoolean())
        {
            Console.WriteLine($"Model '{modelId}' is ready on backend '{backendId}'.");
            return 0;
        }

        var error = switchResult.RootElement.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error";
        Console.WriteLine($"Failed to run model '{modelId}': {error}");
        return 1;
    }

    private static async Task WaitForBackendLegacy(NahelApiClient client, string backendId)
    {
        Console.Write("Waiting for process to start");
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(2000);
            var status = await client.GetAsync($"/engine/{backendId}/status");
            if (status != null)
            {
                var state = status.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;
                if (string.Equals(state, "running", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(" running");
                    return;
                }
            }
            Console.Write(".");
            Console.Out.Flush();
        }
        Console.WriteLine(" timeout");
    }
}

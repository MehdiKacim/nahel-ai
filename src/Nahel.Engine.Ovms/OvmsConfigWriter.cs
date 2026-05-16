using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Nahel.Engine.Ovms;

public sealed class OvmsConfigWriter
{
    private readonly ILogger<OvmsConfigWriter> _logger;

    public OvmsConfigWriter(ILogger<OvmsConfigWriter> logger)
    {
        _logger = logger;
    }

    public async Task WriteConfigAsync(string configPath, IReadOnlyList<OvmsModelConfig> models, CancellationToken ct = default)
    {
        var config = new
        {
            model_config_list = models.Select(m => new
            {
                config = new
                {
                    name = m.Name,
                    base_path = m.BasePath,
                    model_path = m.ModelPath,
                    shape = m.Shape
                }
            }).ToArray()
        };

        var tempPath = configPath + ".tmp";
        var backupPath = configPath + ".backup";

        try
        {
            if (File.Exists(configPath))
                File.Copy(configPath, backupPath, overwrite: true);

            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }), ct);
            File.Move(tempPath, configPath, overwrite: true);
            _logger.LogInformation("OVMS config written to {Path}", configPath);
        }
        catch
        {
            if (File.Exists(backupPath))
                File.Copy(backupPath, configPath, overwrite: true);
            throw;
        }
    }

    public async Task<bool> WaitReadinessAsync(string host, int restPort, TimeSpan timeout, CancellationToken ct)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                var r = await client.GetAsync($"http://{host}:{restPort}/v3/models", ct);
                if (r.IsSuccessStatusCode) return true;
            }
            catch { }
            await Task.Delay(1000, ct);
        }
        return false;
    }
}

public sealed record OvmsModelConfig(string Name, string BasePath, string ModelPath, string? Shape);

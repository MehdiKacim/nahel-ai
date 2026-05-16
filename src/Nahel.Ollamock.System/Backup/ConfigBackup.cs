using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Nahel.Ollamock.System.Backup;

public interface IConfigBackup
{
    Task BackupAsync(string toolName, Dictionary<string, string> envVars, string? filePath = null);
    Task RestoreAsync(string toolName);
    Task<bool> HasBackupAsync(string toolName);
}

public sealed class ConfigBackup : IConfigBackup
{
    private readonly string _backupDir;
    private readonly ILogger<ConfigBackup> _logger;

    public ConfigBackup(ILogger<ConfigBackup> logger)
    {
        _logger = logger;
        _backupDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nahel",
            "Ollamock",
            "Backups");
    }

    public async Task BackupAsync(string toolName, Dictionary<string, string> envVars, string? filePath = null)
    {
        var dir = filePath != null ? Path.GetDirectoryName(filePath) : _backupDir;
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var path = filePath ?? Path.Combine(_backupDir, $"{toolName}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(envVars, options);
        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Backed up configuration for {ToolName} to {Path}", toolName, path);
    }

    public async Task RestoreAsync(string toolName)
    {
        var latest = GetLatestBackupFile(toolName);
        if (latest == null)
        {
            _logger.LogWarning("No backup found for {ToolName}", toolName);
            return;
        }

        var json = await File.ReadAllTextAsync(latest);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (dict != null)
        {
            foreach (var kvp in dict)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value, EnvironmentVariableTarget.User);
            }
        }

        _logger.LogInformation("Restored configuration for {ToolName} from {Path}", toolName, latest);
    }

    public Task<bool> HasBackupAsync(string toolName)
    {
        return Task.FromResult(GetLatestBackupFile(toolName) != null);
    }

    private string? GetLatestBackupFile(string toolName)
    {
        if (!Directory.Exists(_backupDir))
        {
            return null;
        }

        return Directory.GetFiles(_backupDir, $"{toolName}-*.json")
            .OrderByDescending(f => f)
            .FirstOrDefault();
    }
}

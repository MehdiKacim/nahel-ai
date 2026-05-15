using System.Text.Json;

namespace Ollamock.Service.Launchers;

public class ConfigBackup : IConfigBackup
{
    private readonly string _backupDir;
    private readonly ILogger<ConfigBackup> _logger;

    public ConfigBackup(ILogger<ConfigBackup> logger)
    {
        _logger = logger;
        _backupDir = Path.Combine(AppContext.BaseDirectory, "backups");
        Directory.CreateDirectory(_backupDir);
    }

    public async Task BackupAsync(string toolName, Dictionary<string, string> envVars, string? filePath = null)
    {
        var backupPath = Path.Combine(_backupDir, $"{toolName}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var backup = new BackupData
        {
            ToolName = toolName,
            Timestamp = DateTime.UtcNow,
            EnvironmentVariables = envVars.ToDictionary(
                kvp => kvp.Key, 
                kvp => GetEnvVar(kvp.Key) ?? ""
            ),
            FilePath = filePath,
            FileContent = filePath != null && File.Exists(filePath) ? await File.ReadAllTextAsync(filePath) : null
        };

        await File.WriteAllTextAsync(backupPath, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
        _logger.LogInformation("Backup created for {Tool}: {Path}", toolName, backupPath);
    }

    public async Task RestoreAsync(string toolName)
    {
        var backupFiles = Directory.GetFiles(_backupDir, $"{toolName}-*.json")
            .OrderByDescending(f => f)
            .ToList();

        if (!backupFiles.Any())
        {
            _logger.LogWarning("No backup found for {Tool}", toolName);
            return;
        }

        var latestBackup = backupFiles.First();
        var json = await File.ReadAllTextAsync(latestBackup);
        var backup = JsonSerializer.Deserialize<BackupData>(json);

        if (backup == null) return;

        // Restore env vars
        foreach (var (key, value) in backup.EnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.User);
        }

        // Restore file if exists
        if (backup.FilePath != null && backup.FileContent != null)
        {
            await File.WriteAllTextAsync(backup.FilePath, backup.FileContent);
        }

        _logger.LogInformation("Restored backup for {Tool}: {Path}", toolName, latestBackup);
    }

    public Task<bool> HasBackupAsync(string toolName)
    {
        var hasBackup = Directory.GetFiles(_backupDir, $"{toolName}-*.json").Any();
        return Task.FromResult(hasBackup);
    }

    private static string? GetEnvVar(string key)
    {
        return Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(key, EnvironmentVariableTarget.Process);
    }

    private class BackupData
    {
        public string ToolName { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public string? FilePath { get; set; }
        public string? FileContent { get; set; }
    }
}

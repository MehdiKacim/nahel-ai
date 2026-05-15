namespace OllamaBridge.Config;

public class BridgeConfig
{
    public string Listen { get; set; } = "http://localhost:11434";
    public string OllamaNative { get; set; } = "http://localhost:11436";
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public int StartupDelayMs { get; set; } = 500;
    public int MaxBackendRestarts { get; set; } = 3;
    public int LogRetentionDays { get; set; } = 7;
    public int MetricsRetentionDays { get; set; } = 30;
}

public class BackendConfig
{
    public string Id { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Arguments { get; set; } = "";
    public int Port { get; set; }
    public string WorkingDirectory { get; set; } = "";
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public bool AutoStart { get; set; } = true;
    public string HealthEndpoint { get; set; } = "/health";
    public string AdapterType { get; set; } = "openai";
    public string? RuntimeVersion { get; set; }
}

public class ModelConfig
{
    public string BackendId { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public string? MmprojPath { get; set; }
    public int? ContextSize { get; set; }
    public bool? AutoStart { get; set; }
    public string? Device { get; set; }
    public string? FallbackDevice { get; set; }
}

public class AppSettings
{
    public BridgeConfig Bridge { get; set; } = new();
    public Dictionary<string, BackendConfig> Backends { get; set; } = new();
    public Dictionary<string, ModelConfig> Models { get; set; } = new();
}

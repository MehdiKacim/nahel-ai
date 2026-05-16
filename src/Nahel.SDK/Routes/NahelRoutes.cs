namespace Nahel.SDK.Routes;
public static class NahelRoutes
{
    public const string Health = "/health";
    public const string Version = "/version";
    public const string Dashboard = "/dashboard";

    public const string Engines = "/engine";
    public const string EngineStatus = "/engine/{engineId}/status";
    public const string EngineHealth = "/engine/{engineId}/health";
    public const string EngineStart = "/engine/{engineId}/start";
    public const string EngineStop = "/engine/{engineId}/stop";
    public const string EngineRestart = "/engine/{engineId}/restart";
    public const string EngineCapabilities = "/engine/{engineId}/capabilities";
    public const string EngineLogs = "/engine/{engineId}/logs";

    public const string EngineModels = "/engine/{engineId}/models";
    public const string EngineRegisterModel = "/engine/{engineId}/models/register";
    public const string EngineSwitchModel = "/engine/{engineId}/models/switch";
    public const string EngineUnloadModel = "/engine/{engineId}/models/{modelId}/unload";

    public const string EngineUpdate = "/engine/{engineId}/update";
    public const string RuntimeUpdate = "/engine/{engineId}/runtime/update";

    public const string Jobs = "/jobs";
    public const string JobById = "/jobs/{jobId}";

    public const string Tools = "/tools";
    public const string ToolStatus = "/tools/{toolId}/status";
    public const string ToolStart = "/tools/{toolId}/start";
    public const string ToolStop = "/tools/{toolId}/stop";
    public const string ToolLogs = "/tools/{toolId}/logs";

    public const string OpenAiModels = "/v1/models";
    public const string OpenAiChatCompletions = "/v1/chat/completions";
    public const string OpenAiCompletions = "/v1/completions";
    public const string OpenAiEmbeddings = "/v1/embeddings";

    public const string SignalREngineHub = "/hubs/engine";
}

using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IBackendEventBus
{
    event EventHandler<EngineEvent>? EngineEventReceived;
    event EventHandler<LogEvent>? LogReceived;
    void Publish(EngineEvent evt);
    void Publish(LogEvent log);
}

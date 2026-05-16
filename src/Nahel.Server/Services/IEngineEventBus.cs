using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public interface IEngineEventBus
{
    event EventHandler<EngineEvent>? EngineEventReceived;
    event EventHandler<LogEvent>? LogReceived;
    void Publish(EngineEvent evt);
    void Publish(LogEvent log);
}

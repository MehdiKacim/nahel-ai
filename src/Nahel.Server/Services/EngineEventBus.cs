using Nahel.SDK.Models;

namespace Nahel.Server.Services;

public sealed class EngineEventBus : IEngineEventBus
{
    public event EventHandler<EngineEvent>? EngineEventReceived;
    public event EventHandler<LogEvent>? LogReceived;

    public void Publish(EngineEvent evt) => EngineEventReceived?.Invoke(this, evt);
    public void Publish(LogEvent log) => LogReceived?.Invoke(this, log);
}

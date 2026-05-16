namespace Nahel.Engine.Ovms;

public sealed class OvmsLogReader : IDisposable
{
    public event EventHandler<string>? LogLineReceived;

    public void Dispose()
    {
    }
}

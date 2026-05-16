using Microsoft.AspNetCore.SignalR;

namespace Nahel.Server.Hubs;

public sealed class EngineHub : Hub
{
    public async Task SubscribeEngine(string engineId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, engineId);

    public async Task UnsubscribeEngine(string engineId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, engineId);
}

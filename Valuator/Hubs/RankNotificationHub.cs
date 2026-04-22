using Microsoft.AspNetCore.SignalR;

namespace Valuator.Hubs;

public class RankNotificationHub: Hub
{
    public async Task SubscribeToRank(string textId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"rank-{textId}");
    }
}
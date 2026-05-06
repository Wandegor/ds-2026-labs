using Microsoft.AspNetCore.SignalR;

namespace Valuator.Hubs;

public class RankNotificationHub: Hub
{
    public async Task SubscribeToRank(string textId)
    {
        // Клиент(браузер) добавляет себя в виртуальную группу rank-{textId}
        await Groups.AddToGroupAsync(Context.ConnectionId, $"rank-{textId}");
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IntelligentProgrammingPlatform.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    // Қосылымды тек Identity растаған аккаунттың тобына тіркейді; клиент топ таңдамайды.
    public override async Task OnConnectedAsync()
    {
        if (string.IsNullOrEmpty(Context.UserIdentifier)) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, SocialUserGroup.For(Context.UserIdentifier), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}

public static class SocialUserGroup
{
    // Ішкі топ кілтін тек сервер растаған пайдаланушы идентификаторынан құрады.
    public static string For(string userId) => "social-user:" + userId;
}

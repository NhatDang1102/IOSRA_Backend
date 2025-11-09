using System.Security.Claims;
using Main.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Main.SignalR
{
    public class AccountUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return NotificationsHub.GetUserId(connection.User);
        }
    }
}

using System.Threading.Tasks;
using Contract.DTOs.Respond.Notification;
using Main.Hubs;
using Microsoft.AspNetCore.SignalR;
using Service.Interfaces;

namespace Main.SignalR
{
    public class SignalRNotificationDispatcher : INotificationDispatcher
    {
        private readonly IHubContext<NotificationsHub> _hubContext;

        public SignalRNotificationDispatcher(IHubContext<NotificationsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task DispatchAsync(NotificationResponse notification)
        {
            var userId = notification.RecipientId.ToString();
            return _hubContext.Clients.User(userId).SendAsync("notificationReceived", notification);
        }
    }
}

using System.Threading.Tasks;
using Contract.DTOs.Respond.Notification;

namespace Service.Interfaces
{
    public interface INotificationDispatcher
    {
        Task DispatchAsync(NotificationResponse notification);
    }
}

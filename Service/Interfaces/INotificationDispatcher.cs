using System.Threading.Tasks;
using Contract.DTOs.Response.Notification;

namespace Service.Interfaces
{
    public interface INotificationDispatcher
    {
        Task DispatchAsync(NotificationResponse notification);
    }
}

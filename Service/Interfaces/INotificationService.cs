using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Respond.Common;
using Contract.DTOs.Respond.Notification;

namespace Service.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationResponse> CreateAsync(NotificationCreateModel model, CancellationToken ct = default);
        Task<PagedResult<NotificationResponse>> GetAsync(Guid recipientId, int page, int pageSize, CancellationToken ct = default);
        Task MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken ct = default);
        Task MarkAllReadAsync(Guid recipientId, CancellationToken ct = default);
    }

    public record NotificationCreateModel(
        Guid RecipientId,
        string Type,
        string Title,
        string Message,
        object? Payload = null);
}

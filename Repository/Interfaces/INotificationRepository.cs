using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface INotificationRepository
    {
        Task<notification> AddAsync(notification entity, CancellationToken ct = default);
        Task<(IReadOnlyList<notification> Items, int Total)> GetAsync(Guid recipientId, int page, int pageSize, CancellationToken ct = default);
        Task<int> MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken ct = default);
        Task<int> MarkAllReadAsync(Guid recipientId, CancellationToken ct = default);
    }
}

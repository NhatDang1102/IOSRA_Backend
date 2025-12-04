using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAuthorFollowRepository
    {
        Task<follow?> GetAsync(Guid readerId, Guid authorId, CancellationToken ct = default);
        Task<follow> AddAsync(Guid readerId, Guid authorId, bool enableNotifications, CancellationToken ct = default);
        Task RemoveAsync(follow entity, CancellationToken ct = default);
        Task<follow> UpdateNotificationAsync(follow entity, bool enableNotifications, CancellationToken ct = default);
        Task<(IReadOnlyList<AuthorFollowerProjection> Items, int Total)> GetFollowersAsync(Guid authorId, int page, int pageSize, CancellationToken ct = default);
        Task<(IReadOnlyList<AuthorFollowingProjection> Items, int Total)> GetFollowingAsync(Guid readerId, int page, int pageSize, CancellationToken ct = default);
        Task<IReadOnlyList<Guid>> GetFollowerIdsForNotificationsAsync(Guid authorId, CancellationToken ct = default);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Follow;
using Contract.DTOs.Respond.Common;
using Contract.DTOs.Respond.Follow;

namespace Service.Interfaces
{
    public interface IAuthorFollowService
    {
        Task<AuthorFollowStatusResponse> FollowAsync(Guid readerAccountId, Guid authorAccountId, AuthorFollowRequest request, CancellationToken ct = default);
        Task UnfollowAsync(Guid readerAccountId, Guid authorAccountId, CancellationToken ct = default);
        Task<AuthorFollowStatusResponse> UpdateNotificationAsync(Guid readerAccountId, Guid authorAccountId, bool enableNotifications, CancellationToken ct = default);
        Task<PagedResult<AuthorFollowerResponse>> GetFollowersAsync(Guid authorAccountId, int page, int pageSize, CancellationToken ct = default);
    }
}

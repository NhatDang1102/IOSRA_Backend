using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Story;
using Contract.DTOs.Response.Common;

namespace Service.Interfaces
{
    public interface IFavoriteStoryService
    {
        Task<FavoriteStoryResponse> AddAsync(Guid readerId, Guid storyId, CancellationToken ct = default);
        Task RemoveAsync(Guid readerId, Guid storyId, CancellationToken ct = default);
        Task<FavoriteStoryResponse> ToggleNotificationAsync(Guid readerId, Guid storyId, FavoriteStoryNotificationRequest request, CancellationToken ct = default);
        Task<PagedResult<FavoriteStoryResponse>> ListAsync(Guid readerId, int page, int pageSize, CancellationToken ct = default);
    }
}

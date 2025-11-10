using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Common;
using Contract.DTOs.Respond.Story;

namespace Service.Interfaces
{
    public interface IStoryCatalogService
    {
        Task<PagedResult<StoryCatalogListItemResponse>> GetStoriesAsync(StoryCatalogQuery query, CancellationToken ct = default);
        Task<StoryCatalogDetailResponse> GetStoryAsync(Guid storyId, CancellationToken ct = default);
        Task<PagedResult<StoryCatalogListItemResponse>> GetStoriesAdvancedAsync(StoryCatalogQuery query, CancellationToken ct = default);
    }
}

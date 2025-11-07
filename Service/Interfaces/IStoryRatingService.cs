using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;

namespace Service.Interfaces
{
    public interface IStoryRatingService
    {
        Task<StoryRatingSummaryResponse> GetAsync(Guid storyId, Guid? viewerId, int page, int pageSize, CancellationToken ct = default);
        Task<StoryRatingItemResponse> UpsertAsync(Guid readerAccountId, Guid storyId, StoryRatingRequest request, CancellationToken ct = default);
        Task RemoveAsync(Guid readerAccountId, Guid storyId, CancellationToken ct = default);
    }
}

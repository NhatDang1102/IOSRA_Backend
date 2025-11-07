using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;
using Repository.DataModels;

namespace Repository.Interfaces
{
    public interface IStoryRatingRepository
    {
        Task<story_rating?> GetAsync(Guid storyId, Guid readerId, CancellationToken ct = default);
        Task<story_rating?> GetDetailsAsync(Guid storyId, Guid readerId, CancellationToken ct = default);
        Task AddAsync(story_rating rating, CancellationToken ct = default);
        Task UpdateAsync(story_rating rating, CancellationToken ct = default);
        Task DeleteAsync(story_rating rating, CancellationToken ct = default);
        Task<(List<story_rating> Items, int Total)> GetRatingsPageAsync(Guid storyId, int page, int pageSize, CancellationToken ct = default);
        Task<StoryRatingSummaryData> GetSummaryAsync(Guid storyId, CancellationToken ct = default);
    }
}

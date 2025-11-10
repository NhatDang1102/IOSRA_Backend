using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IStoryCatalogRepository
    {
        Task<(List<story> Items, int Total)> SearchPublishedStoriesAsync(string? query, Guid? tagId, Guid? authorId, int page, int pageSize, CancellationToken ct = default);
        Task<List<story>> GetLatestPublishedStoriesAsync(int limit, CancellationToken ct = default);
        Task<List<story>> GetStoriesByIdsAsync(IEnumerable<Guid> storyIds, CancellationToken ct = default);
        Task<story?> GetPublishedStoryByIdAsync(Guid storyId, CancellationToken ct = default);
        Task<(List<story> Items, int Total)> SearchPublishedStoriesAdvancedAsync(string? query, Guid? tagId, Guid? authorId, bool? isPremium, double? minAvgRating, string? sortBy, 
            bool sortDesc, DateTime? weekStartUtc, int page, int pageSize, CancellationToken ct = default);
    }
}


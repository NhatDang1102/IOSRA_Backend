using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IChapterCatalogRepository
    {
        Task<(List<chapter> Items, int Total)> GetPublishedChaptersByStoryAsync(Guid storyId, int page, int pageSize, CancellationToken ct = default);
        Task<chapter?> GetPublishedChapterByIdAsync(Guid chapterId, CancellationToken ct = default);
        Task<Dictionary<Guid, int>> GetPublishedChapterCountsByStoryIdsAsync(IEnumerable<Guid> storyIds, CancellationToken ct = default);
        Task<int> GetPublishedChapterCountAsync(Guid storyId, CancellationToken ct = default);
    }
}


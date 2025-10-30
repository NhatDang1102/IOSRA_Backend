using Repository.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Repository.Interfaces
{
    public interface IStoryRepository
    {
        Task<author?> GetAuthorAsync(ulong accountId, CancellationToken ct = default);
        Task<List<tag>> GetTagsByIdsAsync(IEnumerable<uint> tagIds, CancellationToken ct = default);
        Task<story> AddStoryAsync(story entity, IEnumerable<uint> tagIds, CancellationToken ct = default);
        Task<List<story>> GetStoriesByAuthorAsync(ulong authorId, CancellationToken ct = default);
        Task<story?> GetStoryWithDetailsAsync(ulong storyId, CancellationToken ct = default);
        Task<story?> GetStoryForAuthorAsync(ulong storyId, ulong authorId, CancellationToken ct = default);
        Task UpdateStoryAsync(story entity, CancellationToken ct = default);
        Task ReplaceStoryTagsAsync(ulong storyId, IEnumerable<uint> tagIds, CancellationToken ct = default);
        Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default);
        Task<content_approve?> GetLatestContentApproveAsync(ulong storyId, string source, CancellationToken ct = default);
        Task<List<content_approve>> GetContentApprovalsForStoryAsync(ulong storyId, CancellationToken ct = default);
        Task<List<story>> GetStoriesPendingHumanReviewAsync(CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}


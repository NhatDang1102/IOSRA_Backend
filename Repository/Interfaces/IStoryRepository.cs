using Repository.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Repository.Interfaces
{
    public interface IStoryRepository
    {
        Task<author?> GetAuthorAsync(Guid accountId, CancellationToken ct = default);
        Task<List<tag>> GetTagsByIdsAsync(IEnumerable<Guid> tagIds, CancellationToken ct = default);
        Task<story> AddStoryAsync(story entity, IEnumerable<Guid> tagIds, CancellationToken ct = default);
        Task<List<story>> GetStoriesByAuthorAsync(Guid authorId, IEnumerable<string>? statuses = null, CancellationToken ct = default);
        Task<story?> GetStoryWithDetailsAsync(Guid storyId, CancellationToken ct = default);
        Task<story?> GetStoryForAuthorAsync(Guid storyId, Guid authorId, CancellationToken ct = default);
        Task UpdateStoryAsync(story entity, CancellationToken ct = default);
        Task ReplaceStoryTagsAsync(Guid storyId, IEnumerable<Guid> tagIds, CancellationToken ct = default);
        Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default);
        Task<content_approve?> GetContentApprovalForStoryAsync(Guid storyId, CancellationToken ct = default);
        Task<content_approve?> GetContentApprovalByIdAsync(Guid reviewId, CancellationToken ct = default);
        Task<List<content_approve>> GetContentApprovalsForStoryAsync(Guid storyId, CancellationToken ct = default);
        Task<List<story>> GetStoriesForModerationAsync(IEnumerable<string> statuses, CancellationToken ct = default);
        Task<(List<story> Items, int Total)> SearchPublishedStoriesAsync(string? query, Guid? tagId, Guid? authorId, int page, int pageSize, CancellationToken ct = default);
        Task<story?> GetPublishedStoryByIdAsync(Guid storyId, CancellationToken ct = default);
        Task<bool> AuthorHasPendingStoryAsync(Guid authorId, Guid? excludeStoryId = null, CancellationToken ct = default);
        Task<bool> AuthorHasUncompletedPublishedStoryAsync(Guid authorId, CancellationToken ct = default);
        Task<DateTime?> GetLastStoryRejectedAtAsync(Guid storyId, CancellationToken ct = default);
        Task<DateTime?> GetLastAuthorStoryRejectedAtAsync(Guid authorId, CancellationToken ct = default);
        Task<int> GetChapterCountAsync(Guid storyId, CancellationToken ct = default);
        Task<DateTime?> GetStoryPublishedAtAsync(Guid storyId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}


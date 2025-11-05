using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAuthorStoryRepository
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
        Task<List<content_approve>> GetContentApprovalsForStoryAsync(Guid storyId, CancellationToken ct = default);

        Task<bool> AuthorHasPendingStoryAsync(Guid authorId, Guid? excludeStoryId = null, CancellationToken ct = default);
        Task<bool> AuthorHasUncompletedPublishedStoryAsync(Guid authorId, CancellationToken ct = default);
        Task<DateTime?> GetLastStoryRejectedAtAsync(Guid storyId, CancellationToken ct = default);
        Task<DateTime?> GetLastAuthorStoryRejectedAtAsync(Guid authorId, CancellationToken ct = default);

        Task<int> GetChapterCountAsync(Guid storyId, CancellationToken ct = default);
        Task<DateTime?> GetStoryPublishedAtAsync(Guid storyId, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}


using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAuthorChapterRepository
    {
        Task<chapter> CreateAsync(chapter entity, CancellationToken ct = default);
        Task<chapter?> GetByIdAsync(Guid chapterId, CancellationToken ct = default);
        Task<chapter?> GetByIdForAuthorAsync(Guid storyId, Guid chapterId, Guid authorId, CancellationToken ct = default);
        Task<IReadOnlyList<chapter>> GetAllByStoryAsync(Guid storyId, IEnumerable<string>? statuses = null, CancellationToken ct = default);
        Task<language_list?> GetLanguageByCodeAsync(string code, CancellationToken ct = default);
        Task<bool> HasPendingChapterAsync(Guid storyId, CancellationToken ct = default);
        Task<int> GetNextChapterNumberAsync(Guid storyId, CancellationToken ct = default);
        Task<bool> HasDraftChapterBeforeAsync(Guid storyId, DateTime createdAt, Guid currentChapterId, CancellationToken ct = default);

        Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default);
        Task<IReadOnlyList<content_approve>> GetContentApprovalsForChapterAsync(Guid chapterId, CancellationToken ct = default);
        Task<content_approve?> GetContentApprovalForChapterAsync(Guid chapterId, CancellationToken ct = default);

        Task UpdateAsync(chapter entity, CancellationToken ct = default);
        Task<DateTime?> GetLastRejectedAtAsync(Guid chapterId, CancellationToken ct = default);
        Task<DateTime?> GetLastAuthorChapterRejectedAtAsync(Guid authorId, CancellationToken ct = default);

        Task<IReadOnlyList<chapter>> GetChaptersMissingSummaryAsync(int limit = 10, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}


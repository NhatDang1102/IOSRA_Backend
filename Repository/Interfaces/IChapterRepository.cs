using System;
using Repository.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Repository.Interfaces
{
    public interface IChapterRepository
    {
        Task<chapter> AddAsync(chapter entity, CancellationToken ct = default);
        Task<chapter?> GetByIdAsync(ulong chapterId, CancellationToken ct = default);
        Task<chapter?> GetForAuthorAsync(ulong storyId, ulong chapterId, ulong authorId, CancellationToken ct = default);
        Task<IReadOnlyList<chapter>> GetByStoryAsync(ulong storyId, CancellationToken ct = default);
        Task<language_list?> GetLanguageByCodeAsync(string code, CancellationToken ct = default);
        Task<IReadOnlyList<chapter>> GetPendingForModerationAsync(CancellationToken ct = default);
        Task<bool> StoryHasPendingChapterAsync(ulong storyId, CancellationToken ct = default);
        Task<int> GetNextChapterNumberAsync(ulong storyId, CancellationToken ct = default);
        Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default);
        Task<IReadOnlyList<content_approve>> GetContentApprovalsForChapterAsync(ulong chapterId, CancellationToken ct = default);
        Task UpdateAsync(chapter entity, CancellationToken ct = default);
        Task<DateTime?> GetLastRejectedAtAsync(ulong chapterId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

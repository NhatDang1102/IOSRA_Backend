using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;
using Repository.DataModels;

namespace Repository.Interfaces
{
    public interface IChapterCommentRepository
    {
        Task<chapter?> GetChapterWithStoryAsync(Guid chapterId, CancellationToken ct = default);
        Task<(List<chapter_comment> Items, int Total)> GetByChapterAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default);
        Task<(List<chapter_comment> Items, int Total)> GetByStoryAsync(Guid storyId, Guid? chapterId, int page, int pageSize, CancellationToken ct = default);
        Task<(List<chapter_comment> Items, int Total)> GetForModerationAsync(string? status, Guid? storyId, Guid? chapterId, Guid? readerId, int page, int pageSize, CancellationToken ct = default);
        Task<chapter_comment?> GetAsync(Guid chapterId, Guid commentId, CancellationToken ct = default);
        Task<chapter_comment?> GetForOwnerAsync(Guid chapterId, Guid commentId, Guid readerId, CancellationToken ct = default);
        Task AddAsync(chapter_comment comment, CancellationToken ct = default);
        Task UpdateAsync(chapter_comment comment, CancellationToken ct = default);
        Task<Dictionary<Guid, ChapterCommentReactionAggregate>> GetReactionAggregatesAsync(Guid[] commentIds, Guid? viewerAccountId, CancellationToken ct = default);
        Task<chapter_comment_reaction?> GetReactionAsync(Guid commentId, Guid readerId, CancellationToken ct = default);
        Task AddReactionAsync(chapter_comment_reaction reaction, CancellationToken ct = default);
        Task UpdateReactionAsync(chapter_comment_reaction reaction, CancellationToken ct = default);
        Task RemoveReactionAsync(chapter_comment_reaction reaction, CancellationToken ct = default);
        Task<(List<chapter_comment_reaction> Items, int Total)> GetReactionsAsync(Guid commentId, string reactionType, int page, int pageSize, CancellationToken ct = default);
    }
}

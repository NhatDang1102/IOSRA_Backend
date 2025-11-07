using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

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
    }
}

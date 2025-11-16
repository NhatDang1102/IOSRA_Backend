using System;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IModerationRepository
    {
        Task<story?> GetStoryAsync(Guid storyId, CancellationToken ct = default);
        Task UpdateStoryAsync(story entity, CancellationToken ct = default);
        Task<chapter?> GetChapterAsync(Guid chapterId, CancellationToken ct = default);
        Task UpdateChapterAsync(chapter entity, CancellationToken ct = default);
        Task<chapter_comment?> GetCommentAsync(Guid commentId, CancellationToken ct = default);
        Task UpdateCommentAsync(chapter_comment entity, CancellationToken ct = default);
    }
}

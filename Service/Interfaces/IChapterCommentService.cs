using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Contract.DTOs.Respond.Common;

namespace Service.Interfaces
{
    public interface IChapterCommentService
    {
        Task<PagedResult<ChapterCommentResponse>> GetByChapterAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default, Guid? viewerAccountId = null);
        Task<StoryCommentFeedResponse> GetByStoryAsync(Guid storyId, Guid? chapterId, int page, int pageSize, CancellationToken ct = default, Guid? viewerAccountId = null);
        Task<PagedResult<ChapterCommentModerationResponse>> GetForModerationAsync(string? status, Guid? storyId, Guid? chapterId, Guid? readerId, int page, int pageSize, CancellationToken ct = default);
        Task<ChapterCommentResponse> CreateAsync(Guid readerAccountId, Guid chapterId, ChapterCommentCreateRequest request, CancellationToken ct = default);
        Task<ChapterCommentResponse> UpdateAsync(Guid readerAccountId, Guid chapterId, Guid commentId, ChapterCommentUpdateRequest request, CancellationToken ct = default);
        Task DeleteAsync(Guid readerAccountId, Guid chapterId, Guid commentId, CancellationToken ct = default);
        Task<ChapterCommentResponse> ReactAsync(Guid readerAccountId, Guid chapterId, Guid commentId, ChapterCommentReactRequest request, CancellationToken ct = default);
        Task<ChapterCommentResponse> RemoveReactionAsync(Guid readerAccountId, Guid chapterId, Guid commentId, CancellationToken ct = default);
        Task<PagedResult<ChapterCommentReactionUserResponse>> GetReactionsAsync(Guid chapterId, Guid commentId, string reactionType, int page, int pageSize, CancellationToken ct = default);
    }
}

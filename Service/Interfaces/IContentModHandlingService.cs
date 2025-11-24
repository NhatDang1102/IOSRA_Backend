using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Moderation;
using Contract.DTOs.Response.Moderation;

namespace Service.Interfaces
{
    public interface IContentModHandlingService
    {
        Task<ModerationStatusResponse> UpdateStoryStatusAsync(Guid moderatorAccountId, Guid storyId, ContentStatusUpdateRequest request, CancellationToken ct = default);
        Task<ModerationStatusResponse> UpdateChapterStatusAsync(Guid moderatorAccountId, Guid chapterId, ContentStatusUpdateRequest request, CancellationToken ct = default);
        Task<ModerationStatusResponse> UpdateCommentStatusAsync(Guid moderatorAccountId, Guid commentId, ContentStatusUpdateRequest request, CancellationToken ct = default);
        Task OverrideStrikeAsync(Guid targetAccountId, StrikeStatusUpdateRequest request, CancellationToken ct = default);
    }
}

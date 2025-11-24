using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IChapterModerationService
    {
        Task<IReadOnlyList<ChapterModerationQueueItem>> ListAsync(string? status = null, CancellationToken ct = default);
        Task<ChapterModerationQueueItem> GetAsync(Guid reviewId, CancellationToken ct = default);
        Task ModerateAsync(Guid moderatorAccountId, Guid reviewId, ChapterModerationDecisionRequest request, CancellationToken ct = default);
    }
}

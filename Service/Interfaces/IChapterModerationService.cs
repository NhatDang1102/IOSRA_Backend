using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IChapterModerationService
    {
        Task<IReadOnlyList<ChapterModerationQueueItem>> ListPendingAsync(CancellationToken ct = default);
        Task ModerateAsync(Guid moderatorAccountId, Guid chapterId, ChapterModerationDecisionRequest request, CancellationToken ct = default);
    }
}

using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IChapterModerationService
    {
        Task<IReadOnlyList<ChapterModerationQueueItem>> ListPendingAsync(CancellationToken ct = default);
        Task ModerateAsync(ulong moderatorAccountId, ulong chapterId, ChapterModerationDecisionRequest request, CancellationToken ct = default);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;

namespace Service.Interfaces
{
    public interface IStoryViewTracker
    {
        Task RecordViewAsync(Guid storyId, Guid? viewerAccountId, string? viewerFingerprint, CancellationToken ct = default);
        Task<IReadOnlyList<StoryViewCount>> GetWeeklyTopAsync(DateTime weekStartUtc, int limit, CancellationToken ct = default);
        Task<IReadOnlyList<StoryViewCount>> GetWeeklyViewsAsync(DateTime weekStartUtc, CancellationToken ct = default);
        DateTime GetCurrentWeekStartUtc();
    }
}

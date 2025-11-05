using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;

namespace Repository.Interfaces
{
    public interface IStoryWeeklyViewRepository
    {
        Task UpsertWeeklyViewsAsync(DateTime weekStartUtc, IReadOnlyCollection<StoryViewCount> items, CancellationToken ct = default);
        Task<IReadOnlyList<StoryViewCount>> GetTopWeeklyViewsAsync(DateTime weekStartUtc, int limit, CancellationToken ct = default);
    }
}

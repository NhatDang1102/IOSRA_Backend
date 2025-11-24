using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Story;

namespace Service.Interfaces
{
    public interface IStoryHighlightService
    {
        Task<IReadOnlyList<StoryCatalogListItemResponse>> GetLatestStoriesAsync(int limit, CancellationToken ct = default);
        Task<IReadOnlyList<StoryWeeklyHighlightResponse>> GetTopWeeklyStoriesAsync(int limit, CancellationToken ct = default);
    }
}


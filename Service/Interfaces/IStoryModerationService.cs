using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Story;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IStoryModerationService
    {
        Task<IReadOnlyList<StoryModerationQueueItem>> ListAsync(string? status = null, CancellationToken ct = default);
        Task<StoryModerationQueueItem> GetAsync(Guid reviewId, CancellationToken ct = default);
        Task ModerateAsync(Guid moderatorAccountId, Guid reviewId, StoryModerationDecisionRequest request, CancellationToken ct = default);
    }
}

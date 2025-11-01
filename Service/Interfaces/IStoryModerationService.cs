using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IStoryModerationService
    {
        Task<IReadOnlyList<StoryModerationQueueItem>> ListPendingAsync(CancellationToken ct = default);
        Task ModerateAsync(Guid moderatorAccountId, Guid storyId, StoryModerationDecisionRequest request, CancellationToken ct = default);
    }
}

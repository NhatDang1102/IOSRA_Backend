using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IStoryService
    {
        Task<StoryResponse> CreateAsync(Guid authorAccountId, StoryCreateRequest request, CancellationToken ct = default);
        Task<StoryResponse> SubmitForReviewAsync(Guid authorAccountId, Guid storyId, StorySubmitRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<StoryListItemResponse>> ListAsync(Guid authorAccountId, CancellationToken ct = default);
        Task<StoryResponse> GetAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default);
        Task<StoryResponse> CompleteAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default);
    }
}

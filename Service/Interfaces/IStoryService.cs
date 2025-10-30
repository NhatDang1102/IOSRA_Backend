using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IStoryService
    {
        Task<StoryResponse> CreateAsync(ulong authorAccountId, StoryCreateRequest request, CancellationToken ct = default);
        Task<StoryResponse> SubmitForReviewAsync(ulong authorAccountId, ulong storyId, StorySubmitRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<StoryListItemResponse>> ListAsync(ulong authorAccountId, CancellationToken ct = default);
        Task<StoryResponse> GetAsync(ulong authorAccountId, ulong storyId, CancellationToken ct = default);
    }
}


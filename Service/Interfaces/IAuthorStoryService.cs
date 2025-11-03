using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IAuthorStoryService
    {
        Task<StoryResponse> CreateAsync(Guid authorAccountId, StoryCreateRequest request, CancellationToken ct = default);
        Task<StoryResponse> SubmitForReviewAsync(Guid authorAccountId, Guid storyId, StorySubmitRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<StoryListItemResponse>> ListAsync(Guid authorAccountId, string? status = null, CancellationToken ct = default);
        Task<StoryResponse> GetAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default);
        Task<StoryResponse> CompleteAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default);
        Task<StoryResponse> UpdateCoverAsync(Guid authorAccountId, Guid storyId, IFormFile coverFile, CancellationToken ct = default);
    }
}

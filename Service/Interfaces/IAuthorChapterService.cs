using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IAuthorChapterService
    {
        Task<ChapterResponse> CreateAsync(Guid authorAccountId, Guid storyId, ChapterCreateRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<ChapterListItemResponse>> GetAllAsync(Guid authorAccountId, Guid storyId, string? status = null, CancellationToken ct = default);
        Task<ChapterResponse> GetByIdAsync(Guid authorAccountId, Guid storyId, Guid chapterId, CancellationToken ct = default);
        Task<ChapterResponse> UpdateDraftAsync(Guid authorAccountId, Guid storyId, Guid chapterId, ChapterUpdateRequest request, CancellationToken ct = default);
        Task<ChapterResponse> SubmitAsync(Guid authorAccountId, Guid chapterId, ChapterSubmitRequest request, CancellationToken ct = default);
        Task<ChapterResponse> WithdrawAsync(Guid authorAccountId, Guid chapterId, CancellationToken ct = default);
    }
}

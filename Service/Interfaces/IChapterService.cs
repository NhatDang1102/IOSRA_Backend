using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IChapterService
    {
        Task<ChapterResponse> CreateAsync(Guid authorAccountId, Guid storyId, ChapterCreateRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<ChapterListItemResponse>> ListAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default);
        Task<ChapterResponse> GetAsync(Guid authorAccountId, Guid storyId, Guid chapterId, CancellationToken ct = default);
        Task<ChapterResponse> SubmitAsync(Guid authorAccountId, Guid chapterId, ChapterSubmitRequest request, CancellationToken ct = default);
    }
}

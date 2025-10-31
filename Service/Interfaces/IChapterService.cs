using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IChapterService
    {
        Task<ChapterResponse> CreateAsync(ulong authorAccountId, ulong storyId, ChapterCreateRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<ChapterListItemResponse>> ListAsync(ulong authorAccountId, ulong storyId, CancellationToken ct = default);
        Task<ChapterResponse> GetAsync(ulong authorAccountId, ulong storyId, ulong chapterId, CancellationToken ct = default);
        Task<ChapterResponse> SubmitAsync(ulong authorAccountId, ulong chapterId, ChapterSubmitRequest request, CancellationToken ct = default);
    }
}

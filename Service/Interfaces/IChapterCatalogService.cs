using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Contract.DTOs.Respond.Common;

namespace Service.Interfaces
{
    public interface IChapterCatalogService
    {
        Task<PagedResult<ChapterCatalogListItemResponse>> GetChaptersAsync(ChapterCatalogQuery query, CancellationToken ct = default);
        Task<ChapterCatalogDetailResponse> GetChapterAsync(Guid chapterId, CancellationToken ct = default);
    }
}

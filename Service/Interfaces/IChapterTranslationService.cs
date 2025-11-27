using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;

namespace Service.Interfaces
{
    public interface IChapterTranslationService
    {
        Task<ChapterTranslationResponse> TranslateAsync(Guid chapterId, ChapterTranslationRequest request, Guid requesterAccountId, CancellationToken ct = default);
        Task<ChapterTranslationResponse> GetAsync(Guid chapterId, string languageCode, Guid? viewerAccountId, CancellationToken ct = default);
        Task<ChapterTranslationStatusResponse> GetStatusesAsync(Guid chapterId, Guid? viewerAccountId, CancellationToken ct = default);
    }
}

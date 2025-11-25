using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IChapterCatalogRepository
    {
        Task<(List<chapter> Items, int Total)> GetPublishedChaptersByStoryAsync(Guid storyId, int page, int pageSize, CancellationToken ct = default);
        Task<chapter?> GetPublishedChapterByIdAsync(Guid chapterId, CancellationToken ct = default);
        Task<chapter?> GetPublishedChapterWithVoicesAsync(Guid chapterId, CancellationToken ct = default);
        Task<Dictionary<Guid, int>> GetPublishedChapterCountsByStoryIdsAsync(IEnumerable<Guid> storyIds, CancellationToken ct = default);
        Task<int> GetPublishedChapterCountAsync(Guid storyId, CancellationToken ct = default);
        Task<bool> HasReaderPurchasedChapterAsync(Guid chapterId, Guid readerId, CancellationToken ct = default);
        Task<language_list?> GetLanguageByCodeAsync(string languageCode, CancellationToken ct = default);
        Task<chapter_localization?> GetLocalizationAsync(Guid chapterId, Guid langId, CancellationToken ct = default);
        Task AddLocalizationAsync(chapter_localization entity, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

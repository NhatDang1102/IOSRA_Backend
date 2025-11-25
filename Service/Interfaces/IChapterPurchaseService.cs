using System;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;

namespace Service.Interfaces
{
    public interface IChapterPurchaseService
    {
        Task<ChapterPurchaseResponse> PurchaseAsync(Guid readerAccountId, Guid chapterId, CancellationToken ct = default);
        Task<ChapterVoicePurchaseResponse> PurchaseVoicesAsync(Guid readerAccountId, Guid chapterId, ChapterVoicePurchaseRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<PurchasedChapterResponse>> GetPurchasedChaptersAsync(Guid readerAccountId, Guid? storyId, CancellationToken ct = default);
        Task<IReadOnlyList<PurchasedVoiceResponse>> GetPurchasedVoicesAsync(Guid readerAccountId, Guid chapterId, CancellationToken ct = default);
        Task<IReadOnlyList<PurchasedVoiceHistoryResponse>> GetPurchasedVoiceHistoryAsync(Guid readerAccountId, CancellationToken ct = default);
    }
}

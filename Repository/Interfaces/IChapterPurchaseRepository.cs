using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Repository.DataModels;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IChapterPurchaseRepository
    {
        Task<chapter?> GetChapterForPurchaseAsync(Guid chapterId, CancellationToken ct = default);
        Task<chapter?> GetChapterWithVoicesAsync(Guid chapterId, CancellationToken ct = default);
        Task<bool> HasReaderPurchasedChapterAsync(Guid chapterId, Guid readerId, CancellationToken ct = default);
        Task<IReadOnlyList<Guid>> GetPurchasedVoiceIdsAsync(Guid chapterId, Guid readerId, CancellationToken ct = default);
        Task AddVoicePurchaseLogAsync(voice_purchase_log entity, CancellationToken ct = default);
        Task AddVoicePurchaseItemAsync(voice_purchase_item entity, CancellationToken ct = default);
        Task<IReadOnlyList<PurchasedChapterData>> GetPurchasedChaptersAsync(Guid readerId, Guid? storyId, CancellationToken ct = default);
        Task<IReadOnlyList<PurchasedVoiceData>> GetPurchasedVoicesAsync(Guid readerId, Guid chapterId, CancellationToken ct = default);
        Task<IReadOnlyList<PurchasedVoiceData>> GetPurchasedVoicesAsync(Guid readerId, IReadOnlyCollection<Guid> chapterIds, CancellationToken ct = default);
        Task<IReadOnlyList<PurchasedVoiceData>> GetPurchasedVoicesAsync(Guid readerId, CancellationToken ct = default);
        Task AddPurchaseLogAsync(chapter_purchase_log entity, CancellationToken ct = default);
        Task AddAuthorRevenueTransactionAsync(author_revenue_transaction entity, CancellationToken ct = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

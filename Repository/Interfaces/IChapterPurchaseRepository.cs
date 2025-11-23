using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IChapterPurchaseRepository
    {
        Task<chapter?> GetChapterForPurchaseAsync(Guid chapterId, CancellationToken ct = default);
        Task<bool> HasReaderPurchasedChapterAsync(Guid chapterId, Guid readerId, CancellationToken ct = default);
        Task AddPurchaseLogAsync(chapter_purchase_log entity, CancellationToken ct = default);
        Task AddAuthorRevenueTransactionAsync(author_revenue_transaction entity, CancellationToken ct = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

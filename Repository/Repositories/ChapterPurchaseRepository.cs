using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class ChapterPurchaseRepository : BaseRepository, IChapterPurchaseRepository
    {
        public ChapterPurchaseRepository(AppDbContext db) : base(db)
        {
        }

        public Task<chapter?> GetChapterForPurchaseAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapters
                .Include(c => c.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.rank)
                .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public Task<bool> HasReaderPurchasedChapterAsync(Guid chapterId, Guid readerId, CancellationToken ct = default)
            => _db.chapter_purchase_logs.AnyAsync(p => p.chapter_id == chapterId && p.account_id == readerId, ct);

        public Task AddPurchaseLogAsync(chapter_purchase_log entity, CancellationToken ct = default)
        {
            _db.chapter_purchase_logs.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddAuthorRevenueTransactionAsync(author_revenue_transaction entity, CancellationToken ct = default)
        {
            _db.author_revenue_transactions.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => _db.Database.BeginTransactionAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

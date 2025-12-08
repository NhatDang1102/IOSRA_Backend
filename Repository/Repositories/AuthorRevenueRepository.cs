using System;
using System.Collections.Generic;
using System.Linq;
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
    public class AuthorRevenueRepository : BaseRepository, IAuthorRevenueRepository
    {
        public AuthorRevenueRepository(AppDbContext db) : base(db)
        {
        }

        public Task<author?> GetAuthorAsync(Guid authorAccountId, CancellationToken ct = default)
            => _db.authors
                  .Include(a => a.rank)
                  .FirstOrDefaultAsync(a => a.account_id == authorAccountId, ct);

        public async Task<(List<author_revenue_transaction> Items, int Total)> GetTransactionsAsync(Guid authorAccountId, int page, int pageSize, string? type, DateTime? from, DateTime? to, CancellationToken ct = default)
        {
            //đọc transaction trong db
            IQueryable<author_revenue_transaction> query = _db.author_revenue_transaction
                .AsNoTracking()
                .Where(t => t.author_id == authorAccountId);
            //các filter 
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(t => t.type == type);
            }

            if (from.HasValue)
            {
                query = query.Where(t => t.created_at >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(t => t.created_at <= to.Value);
            }
            //trả cả lịch sử ua voice, mua chapter trong transation này luôn, map sang chapter liên quan luôn
            query = query
                .Include(t => t.purchase_log)
                    .ThenInclude(pl => pl!.chapter);

            query = query
                .Include(t => t.voice_purchase)
                    .ThenInclude(vp => vp!.chapter);

            query = query
                .Include(t => t.voice_purchase)
                    .ThenInclude(vp => vp!.voice_purchase_items)
                        .ThenInclude(i => i.voice);

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(t => t.created_at)
                .ThenByDescending(t => t.trans_id)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public Task AddTransactionAsync(author_revenue_transaction transaction, CancellationToken ct = default)
        {
            _db.author_revenue_transaction.Add(transaction);
            return Task.CompletedTask;
        }

        //transaction chỉ thành công khi tất cả thao tác đều thành công, 1 cái fail thì rollback hết 
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => _db.Database.BeginTransactionAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

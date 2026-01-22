using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Repository.Base;
using Repository.DataModels;
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
                    .ThenInclude(pl => pl!.chapter)
                        .ThenInclude(c => c!.story);

            query = query
                .Include(t => t.voice_purchase)
                    .ThenInclude(vp => vp!.chapter)
                        .ThenInclude(c => c!.story);

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

        public async Task<bool> IsStoryOwnedByAuthorAsync(Guid storyId, Guid authorId, CancellationToken ct = default)
        {
            return await _db.stories.AnyAsync(s => s.story_id == storyId && s.author_id == authorId, ct);
        }

        public Task<story?> GetStoryOwnedByAuthorAsync(Guid storyId, Guid authorId, CancellationToken ct = default)
        {
            return _db.stories
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.story_id == storyId && s.author_id == authorId, ct);
        }

        public async Task<bool> IsChapterOwnedByAuthorAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
        {
            return await _db.chapter
                .Include(c => c.story)
                .AnyAsync(c => c.chapter_id == chapterId && c.story!.author_id == authorId, ct);
        }

        public Task<chapter?> GetChapterOwnedByAuthorAsync(Guid chapterId, Guid authorId, CancellationToken ct = default)
        {
            return _db.chapter
                .AsNoTracking()
                .Include(c => c.story)
                .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.story!.author_id == authorId, ct);
        }

        public async Task<(List<RevenuePurchaseLogData> Items, int Total, long TotalRevenue, long ChapterRevenue, long VoiceRevenue, int TotalChapterCount, int TotalVoiceCount)> GetStoryPurchaseLogsAsync(Guid storyId, int page, int pageSize, CancellationToken ct = default)
        {
            var chapterRevenue = await _db.chapter_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter!.story_id == storyId)
                .SumAsync(log => (long)log.dia_price, ct);

            var voiceRevenue = await _db.voice_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter!.story_id == storyId)
                .SumAsync(log => (long)log.total_dias, ct);

            var chapterCount = await _db.chapter_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter!.story_id == storyId)
                .CountAsync(ct);

            var voiceCount = await _db.voice_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter!.story_id == storyId)
                .CountAsync(ct);

            var chapterPurchases = _db.chapter_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter!.story_id == storyId)
                .Select(log => new RevenuePurchaseLogData
                {
                    AccountId = log.account_id,
                    Username = log.account.username ?? "Unknown",
                    AvatarUrl = log.account.avatar_url,
                    Price = (long)log.dia_price,
                    CreatedAt = log.created_at,
                    Type = "chapter"
                });

            var voicePurchases = _db.voice_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter!.story_id == storyId)
                .Select(log => new RevenuePurchaseLogData
                {
                    AccountId = log.account_id,
                    Username = log.account.username ?? "Unknown",
                    AvatarUrl = log.account.avatar_url,
                    Price = (long)log.total_dias,
                    CreatedAt = log.created_at,
                    Type = "voice"
                });

            var query = chapterPurchases.Union(voicePurchases);

            var total = await query.CountAsync(ct);
            var totalRevenue = chapterRevenue + voiceRevenue;

            var items = await query
                .OrderByDescending(log => log.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total, totalRevenue, chapterRevenue, voiceRevenue, chapterCount, voiceCount);
        }

        public async Task<(List<RevenuePurchaseLogData> Items, int Total, long TotalRevenue, long ChapterRevenue, long VoiceRevenue, int TotalChapterCount, int TotalVoiceCount)> GetChapterPurchaseLogsAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default)
        {
            var chapterRevenue = await _db.chapter_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter_id == chapterId)
                .SumAsync(log => (long)log.dia_price, ct);

            var voiceRevenue = await _db.voice_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter_id == chapterId)
                .SumAsync(log => (long)log.total_dias, ct);

            var chapterCount = await _db.chapter_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter_id == chapterId)
                .CountAsync(ct);

            var voiceCount = await _db.voice_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter_id == chapterId)
                .CountAsync(ct);

            var chapterPurchases = _db.chapter_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter_id == chapterId)
                .Select(log => new RevenuePurchaseLogData
                {
                    AccountId = log.account_id,
                    Username = log.account.username ?? "Unknown",
                    AvatarUrl = log.account.avatar_url,
                    Price = (long)log.dia_price,
                    CreatedAt = log.created_at,
                    Type = "chapter"
                });

            var voicePurchases = _db.voice_purchase_logs
                .AsNoTracking()
                .Where(log => log.chapter_id == chapterId)
                .Select(log => new RevenuePurchaseLogData
                {
                    AccountId = log.account_id,
                    Username = log.account.username ?? "Unknown",
                    AvatarUrl = log.account.avatar_url,
                    Price = (long)log.total_dias,
                    CreatedAt = log.created_at,
                    Type = "voice"
                });

            var query = chapterPurchases.Union(voicePurchases);

            var total = await query.CountAsync(ct);
            var totalRevenue = chapterRevenue + voiceRevenue;

            var items = await query
                .OrderByDescending(log => log.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total, totalRevenue, chapterRevenue, voiceRevenue, chapterCount, voiceCount);
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

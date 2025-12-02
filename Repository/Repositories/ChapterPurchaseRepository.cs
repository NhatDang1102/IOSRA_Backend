using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Repository.Base;
using Repository.DBContext;
using Repository.DataModels;
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
            => _db.chapter
                .Include(c => c.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.rank)
                .Include(c => c.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.account)
                .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public Task<chapter?> GetChapterWithVoicesAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapter
                .Include(c => c.chapter_voices)
                    .ThenInclude(v => v.voice)
                .Include(c => c.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.rank)
                .Include(c => c.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.account)
                .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public Task<bool> HasReaderPurchasedChapterAsync(Guid chapterId, Guid readerId, CancellationToken ct = default)
            => _db.chapter_purchase_logs.AnyAsync(p => p.chapter_id == chapterId && p.account_id == readerId, ct);

        public async Task<IReadOnlyList<Guid>> GetPurchasedVoiceIdsAsync(Guid chapterId, Guid readerId, CancellationToken ct = default)
        {
            return await _db.voice_purchase_items
                .AsNoTracking()
                .Where(v => v.chapter_id == chapterId && v.account_id == readerId)
                .Select(v => v.voice_id)
                .ToListAsync(ct);
        }

        public Task AddPurchaseLogAsync(chapter_purchase_log entity, CancellationToken ct = default)
        {
            _db.chapter_purchase_logs.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddVoicePurchaseLogAsync(voice_purchase_log entity, CancellationToken ct = default)
        {
            _db.voice_purchase_logs.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddVoicePurchaseItemAsync(voice_purchase_item entity, CancellationToken ct = default)
        {
            _db.voice_purchase_items.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddAuthorRevenueTransactionAsync(author_revenue_transaction entity, CancellationToken ct = default)
        {
            _db.author_revenue_transaction.Add(entity);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<PurchasedChapterData>> GetPurchasedChaptersAsync(Guid readerId, Guid? storyId, CancellationToken ct = default)
        {
            var query =
                from log in _db.chapter_purchase_logs.AsNoTracking()
                join ch in _db.chapter.AsNoTracking() on log.chapter_id equals ch.chapter_id
                join st in _db.stories.AsNoTracking() on ch.story_id equals st.story_id
                where log.account_id == readerId
                select new { log, ch, st };

            if (storyId.HasValue)
            {
                query = query.Where(x => x.st.story_id == storyId.Value);
            }

            return await query
                .OrderBy(x => x.st.title)
                .ThenBy(x => x.ch.chapter_no)
                .ThenByDescending(x => x.log.created_at)
                .Select(x => new PurchasedChapterData
                {
                    ChapterPurchaseId = x.log.chapter_purchase_id,
                    ChapterId = x.ch.chapter_id,
                    StoryId = x.st.story_id,
                    StoryTitle = x.st.title,
                    ChapterNo = (int)x.ch.chapter_no,
                    ChapterTitle = x.ch.title,
                    PriceDias = x.log.dia_price,
                    PurchasedAt = x.log.created_at
                })
                .ToListAsync(ct);
        }

        public Task<IReadOnlyList<PurchasedVoiceData>> GetPurchasedVoicesAsync(Guid readerId, Guid chapterId, CancellationToken ct = default)
            => GetPurchasedVoicesAsync(readerId, (IReadOnlyCollection<Guid>)new[] { chapterId }, ct);

        public async Task<IReadOnlyList<PurchasedVoiceData>> GetPurchasedVoicesAsync(
            Guid readerId,
            IReadOnlyCollection<Guid> chapterIds,
            CancellationToken ct = default)
        {
            if (chapterIds == null || chapterIds.Count == 0)
            {
                return Array.Empty<PurchasedVoiceData>();
            }

            var chapterIdSet = chapterIds.Distinct().ToHashSet();
            var result = await BuildVoicePurchaseQuery(readerId)
                .Where(v => chapterIdSet.Contains(v.ChapterId))
                .OrderBy(v => v.StoryTitle)
                .ThenBy(v => v.ChapterNo)
                .ThenBy(v => v.PurchasedAt)
                .ToListAsync(ct);

            return result;
        }

        public async Task<IReadOnlyList<PurchasedVoiceData>> GetPurchasedVoicesAsync(Guid readerId, CancellationToken ct = default)
        {
            var result = await BuildVoicePurchaseQuery(readerId)
                .OrderBy(v => v.StoryTitle)
                .ThenBy(v => v.ChapterNo)
                .ThenBy(v => v.PurchasedAt)
                .ToListAsync(ct);

            return result;
        }

        private IQueryable<PurchasedVoiceData> BuildVoicePurchaseQuery(Guid readerId)
        {
            return from voice in _db.voice_purchase_items.AsNoTracking()
                   join chapter in _db.chapter.AsNoTracking() on voice.chapter_id equals chapter.chapter_id
                   join story in _db.stories.AsNoTracking() on chapter.story_id equals story.story_id
                   join preset in _db.voice_lists.AsNoTracking() on voice.voice_id equals preset.voice_id
                   join generated in _db.chapter_voice.AsNoTracking()
                        on new { voice.chapter_id, voice.voice_id } equals new { generated.chapter_id, generated.voice_id }
                   where voice.account_id == readerId
                   select new PurchasedVoiceData
                   {
                       PurchaseVoiceId = voice.purchase_item_id,
                       ChapterId = chapter.chapter_id,
                       StoryId = story.story_id,
                       StoryTitle = story.title,
                       ChapterNo = (int)chapter.chapter_no,
                       ChapterTitle = chapter.title,
                       VoiceId = voice.voice_id,
                       VoiceName = preset.voice_name,
                       VoiceCode = preset.voice_code,
                       PriceDias = voice.dia_price,
                    AudioUrl = generated.storage_path,
                       PurchasedAt = voice.created_at
                   };
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => _db.Database.BeginTransactionAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

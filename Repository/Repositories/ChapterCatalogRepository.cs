using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class ChapterCatalogRepository : BaseRepository, IChapterCatalogRepository
    {
        private const string PublishedStatus = "published";

        public ChapterCatalogRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<(List<chapter> Items, int Total)> GetPublishedChaptersByStoryAsync(Guid storyId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;

            var query = _db.chapters
                .AsNoTracking()
                .Include(c => c.language)
                .Where(c => c.story_id == storyId && c.status == PublishedStatus)
                .OrderBy(c => c.chapter_no);

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query.Skip(skip).Take(pageSize).ToListAsync(ct);
            return (items, total);
        }

        public Task<chapter?> GetPublishedChapterByIdAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapters
                  .AsNoTracking()
                  .Include(c => c.language)
                  .Include(c => c.story)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.status == PublishedStatus, ct);

        public Task<chapter?> GetPublishedChapterWithVoicesAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapters
                .AsNoTracking()
                .Include(c => c.language)
                .Include(c => c.story)
                .Include(c => c.chapter_voices)
                    .ThenInclude(v => v.voice)
                .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.status == PublishedStatus, ct);

        public Task<chapter_voice?> GetChapterVoiceAsync(Guid chapterId, Guid voiceId, CancellationToken ct = default)
            => _db.chapter_voices
                .AsNoTracking()
                .Include(cv => cv.voice)
                .Include(cv => cv.chapter)
                .Where(cv => cv.chapter_id == chapterId
                             && cv.voice_id == voiceId
                             && cv.chapter.status == PublishedStatus)
                .FirstOrDefaultAsync(ct);

        public async Task<Dictionary<Guid, int>> GetPublishedChapterCountsByStoryIdsAsync(IEnumerable<Guid> storyIds, CancellationToken ct = default)
        {
            var ids = storyIds?.Distinct().ToArray();
            if (ids == null || ids.Length == 0)
            {
                return new Dictionary<Guid, int>();
            }

            return await _db.chapters
                .Where(c => ids.Contains(c.story_id) && c.status == PublishedStatus)
                .GroupBy(c => c.story_id)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
        }

        public Task<int> GetPublishedChapterCountAsync(Guid storyId, CancellationToken ct = default)
            => _db.chapters.CountAsync(c => c.story_id == storyId && c.status == PublishedStatus, ct);

        public Task<bool> HasReaderPurchasedChapterAsync(Guid chapterId, Guid readerId, CancellationToken ct = default)
            => _db.chapter_purchase_logs.AnyAsync(p => p.chapter_id == chapterId && p.account_id == readerId, ct);

        public Task<language_list?> GetLanguageByCodeAsync(string languageCode, CancellationToken ct = default)
            => _db.language_lists.AsNoTracking().FirstOrDefaultAsync(l => l.lang_code == languageCode, ct);

        public Task<chapter_localization?> GetLocalizationAsync(Guid chapterId, Guid langId, CancellationToken ct = default)
            => _db.chapter_localizations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.chapter_id == chapterId && l.lang_id == langId, ct);

        public async Task<IReadOnlyList<chapter_localization>> GetLocalizationsByChapterAsync(Guid chapterId, CancellationToken ct = default)
        {
            return await _db.chapter_localizations
                .AsNoTracking()
                .Include(l => l.lang)
                .Where(l => l.chapter_id == chapterId)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<language_list>> GetLanguagesAsync(CancellationToken ct = default)
        {
            return await _db.language_lists
                .AsNoTracking()
                .OrderBy(l => l.lang_name)
                .ToListAsync(ct);
        }

        public Task AddLocalizationAsync(chapter_localization entity, CancellationToken ct = default)
        {
            _db.chapter_localizations.Add(entity);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

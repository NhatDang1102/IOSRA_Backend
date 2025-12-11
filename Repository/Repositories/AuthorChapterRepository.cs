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
using Repository.Utils;

namespace Repository.Repositories
{
    public class AuthorChapterRepository : BaseRepository, IAuthorChapterRepository
    {
        public AuthorChapterRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<chapter> CreateAsync(chapter entity, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(chapter.chapter_id));
            _db.chapter.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        //tất cả hàm get đều bao gồm: story nào, author nào, language gốc, mood của chapter, các voice trong chapter này
        public Task<chapter?> GetByIdAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapter
                  .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                  .Include(c => c.language)
                  .Include(c => c.mood)
                  .Include(c => c.chapter_voices).ThenInclude(cv => cv.voice)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public Task<chapter?> GetByIdForAuthorAsync(Guid storyId, Guid chapterId, Guid authorId, CancellationToken ct = default)
            => _db.chapter
                  .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                  .Include(c => c.language)
                  .Include(c => c.mood)
                  .Include(c => c.chapter_voices).ThenInclude(cv => cv.voice)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.story_id == storyId && c.story.author_id == authorId, ct);

        public async Task<IReadOnlyList<chapter>> GetAllByStoryAsync(Guid storyId, IEnumerable<string>? statuses = null, CancellationToken ct = default)
        {
            var query = _db.chapter.Where(c => c.story_id == storyId);

            if (statuses is not null)
            {
                var statusList = statuses
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToArray();

                if (statusList.Length > 0)
                {
                    query = query.Where(c => statusList.Contains(c.status.ToLower()));
                }
                else
                {
                    return Array.Empty<chapter>();
                }
            }

            query = query
                .Include(c => c.content_approves)
                .Include(c => c.language)
                .Include(c => c.mood);

            return await query
                .OrderBy(c => c.chapter_no)
                .ToListAsync(ct);
        }

        //get ngôn ngữ trong language_list
        public Task<language_list?> GetLanguageByCodeAsync(string code, CancellationToken ct = default)
        {
            var normalized = (code ?? string.Empty).Trim();
            return _db.language_lists.FirstOrDefaultAsync(l => l.lang_code == normalized, ct);
        }
        
        //check coi có chapter đang pending ko 
        public Task<bool> HasPendingChapterAsync(Guid storyId, CancellationToken ct = default)
            => _db.chapter.AnyAsync(c => c.story_id == storyId && c.status == "pending", ct);
        //đảm bảo chapter no phải liên tiếp 
        public async Task<int> GetNextChapterNumberAsync(Guid storyId, CancellationToken ct = default)
        {
            var max = await _db.chapter
                .Where(c => c.story_id == storyId)
                .MaxAsync(c => (int?)c.chapter_no, ct);
            return (max ?? 0) + 1;
        }

        //check coi có chapter nào draft ko 
        public Task<bool> HasDraftChapterBeforeAsync(Guid storyId, DateTime createdAt, Guid currentChapterId, CancellationToken ct = default)
            => _db.chapter.AnyAsync(c =>
                    c.story_id == storyId
                    && c.chapter_id != currentChapterId
                    && c.status == "draft"
                    && c.created_at < createdAt, ct);

        public async Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(content_approve.review_id));
            entity.created_at = entity.created_at == default ? TimezoneConverter.VietnamNow : entity.created_at;
            _db.content_approves.Add(entity);
            await _db.SaveChangesAsync(ct);
        }
        //này giống bên story, hàm trên lấy hết content approve hàm dưới lấy cái mới nhất 
        public async Task<IReadOnlyList<content_approve>> GetContentApprovalsForChapterAsync(Guid chapterId, CancellationToken ct = default)
        {
            return await _db.content_approves
                .Where(c => c.chapter_id == chapterId && c.approve_type == "chapter")
                .OrderByDescending(c => c.created_at)
                .ToListAsync(ct);
        }

        public Task<content_approve?> GetContentApprovalForChapterAsync(Guid chapterId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.chapter_id == chapterId && c.approve_type == "chapter")
                  .OrderByDescending(c => c.created_at)
                  .FirstOrDefaultAsync(ct);

        public async Task UpdateAsync(chapter entity, CancellationToken ct = default)
        {
            _db.chapter.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        //lấy lần cuối bị rejected để cooldown 
        public Task<DateTime?> GetLastRejectedAtAsync(Guid chapterId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.chapter_id == chapterId && c.approve_type == "chapter" && c.status == "rejected")
                  .OrderByDescending(c => c.created_at)
                  .Select(c => (DateTime?)c.created_at)
                  .FirstOrDefaultAsync(ct);

        public Task<DateTime?> GetLastAuthorChapterRejectedAtAsync(Guid authorId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.approve_type == "chapter" &&
                              c.status == "rejected" &&
                              c.chapter != null &&
                              c.chapter.story != null &&
                              c.chapter.story.author_id == authorId)
                  .OrderByDescending(c => c.created_at)
                  .Select(c => (DateTime?)c.created_at)
                  .FirstOrDefaultAsync(ct);

        public async Task<IReadOnlyList<chapter>> GetChaptersMissingSummaryAsync(int limit = 10, CancellationToken ct = default)
        {
            return await _db.chapter
                .Include(c => c.story)
                .Where(c => c.status == "published" && c.summary == null)
                .OrderByDescending(c => c.created_at)
                .Take(limit)
                .ToListAsync(ct);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

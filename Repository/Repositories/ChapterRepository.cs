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
    public class ChapterRepository : BaseRepository, IChapterRepository
    {
        public ChapterRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<chapter> AddAsync(chapter entity, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(chapter.chapter_id));
            _db.chapters.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public Task<chapter?> GetByIdAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapters
                  .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                  .Include(c => c.language)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public Task<chapter?> GetForAuthorAsync(Guid storyId, Guid chapterId, Guid authorId, CancellationToken ct = default)
            => _db.chapters
                  .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                  .Include(c => c.language)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.story_id == storyId && c.story.author_id == authorId, ct);

        public async Task<IReadOnlyList<chapter>> GetByStoryAsync(Guid storyId, IEnumerable<string>? statuses = null, CancellationToken ct = default)
        {
            var query = _db.chapters
                .Where(c => c.story_id == storyId);

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
                .Include(c => c.language);

            return await query
                .OrderBy(c => c.chapter_no)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<chapter>> GetForModerationAsync(IEnumerable<string> statuses, CancellationToken ct = default)
        {
            var statusList = statuses?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray() ?? Array.Empty<string>();

            if (statusList.Length == 0)
            {
                return Array.Empty<chapter>();
            }

            return await _db.chapters
                .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                .Include(c => c.language)
                .Include(c => c.content_approves)
                .Where(c => statusList.Contains(c.status.ToLower()))
                .OrderByDescending(c => c.submitted_at ?? c.updated_at)
                .ToListAsync(ct);
        }

        public Task<language_list?> GetLanguageByCodeAsync(string code, CancellationToken ct = default)
        {
            var normalized = (code ?? string.Empty).Trim();
            return _db.language_lists.FirstOrDefaultAsync(l => l.lang_code == normalized, ct);
        }

        public Task<bool> StoryHasPendingChapterAsync(Guid storyId, CancellationToken ct = default)
            => _db.chapters.AnyAsync(c => c.story_id == storyId && c.status == "pending", ct);

        public async Task<int> GetNextChapterNumberAsync(Guid storyId, CancellationToken ct = default)
        {
            var max = await _db.chapters
                .Where(c => c.story_id == storyId)
                .MaxAsync(c => (int?)c.chapter_no, ct);
            return (max ?? 0) + 1;
        }

        public async Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(content_approve.review_id));
            entity.created_at = entity.created_at == default ? DateTime.UtcNow : entity.created_at;
            _db.content_approves.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

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

        public Task<content_approve?> GetContentApprovalByIdAsync(Guid reviewId, CancellationToken ct = default)
            => _db.content_approves
                  .Include(c => c.chapter!)
                      .ThenInclude(ch => ch.story!)
                      .ThenInclude(s => s.author!)
                      .ThenInclude(a => a.account)
                  .Include(c => c.chapter!)
                      .ThenInclude(ch => ch.story!)
                      .ThenInclude(s => s.author!)
                      .ThenInclude(a => a.rank)
                  .Include(c => c.chapter!)
                      .ThenInclude(ch => ch.language)
                  .FirstOrDefaultAsync(c => c.review_id == reviewId, ct);

        public async Task UpdateAsync(chapter entity, CancellationToken ct = default)
        {
            _db.chapters.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

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

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}



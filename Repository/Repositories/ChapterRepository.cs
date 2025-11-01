using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        public async Task<IReadOnlyList<chapter>> GetByStoryAsync(Guid storyId, CancellationToken ct = default)
        {
            return await _db.chapters
                .Where(c => c.story_id == storyId)
                .Include(c => c.content_approves)
                .Include(c => c.language)
                .OrderBy(c => c.chapter_no)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<chapter>> GetPendingForModerationAsync(CancellationToken ct = default)
        {
            return await _db.chapters
                .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                .Include(c => c.language)
                .Where(c => c.status == "pending")
                .OrderBy(c => c.submitted_at ?? c.updated_at)
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
            entity.created_at = DateTime.UtcNow;
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

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

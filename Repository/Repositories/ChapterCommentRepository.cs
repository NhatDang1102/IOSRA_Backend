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
    public class ChapterCommentRepository : BaseRepository, IChapterCommentRepository
    {
        private static readonly string[] VisibleStatuses = { "visible" };
        private static readonly string[] AllowedStatuses = { "visible", "hidden", "removed" };

        public ChapterCommentRepository(AppDbContext db) : base(db)
        {
        }

        public Task<chapter?> GetChapterWithStoryAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapters
                  .Include(c => c.story)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public async Task<(List<chapter_comment> Items, int Total)> GetByChapterAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.chapter_comments
                .AsNoTracking()
                .Include(c => c.reader).ThenInclude(r => r.account)
                .Include(c => c.chapter)
                .Where(c => c.chapter_id == chapterId && VisibleStatuses.Contains(c.status));

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(c => c.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<(List<chapter_comment> Items, int Total)> GetByStoryAsync(Guid storyId, Guid? chapterId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.chapter_comments
                .AsNoTracking()
                .Include(c => c.reader).ThenInclude(r => r.account)
                .Include(c => c.chapter)
                .Where(c => c.story_id == storyId && VisibleStatuses.Contains(c.status));

            if (chapterId.HasValue && chapterId.Value != Guid.Empty)
            {
                query = query.Where(c => c.chapter_id == chapterId.Value);
            }

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(c => c.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<(List<chapter_comment> Items, int Total)> GetForModerationAsync(string? status, Guid? storyId, Guid? chapterId, Guid? readerId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.chapter_comments
                .AsNoTracking()
                .Include(c => c.reader).ThenInclude(r => r.account)
                .Include(c => c.chapter).ThenInclude(ch => ch.story)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToLowerInvariant();
                if (AllowedStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.status == normalized);
                }
            }

            if (storyId.HasValue && storyId.Value != Guid.Empty)
            {
                query = query.Where(c => c.story_id == storyId.Value);
            }

            if (chapterId.HasValue && chapterId.Value != Guid.Empty)
            {
                query = query.Where(c => c.chapter_id == chapterId.Value);
            }

            if (readerId.HasValue && readerId.Value != Guid.Empty)
            {
                query = query.Where(c => c.reader_id == readerId.Value);
            }

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(c => c.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public Task<chapter_comment?> GetAsync(Guid chapterId, Guid commentId, CancellationToken ct = default)
            => _db.chapter_comments
                  .Include(c => c.reader).ThenInclude(r => r.account)
                  .Include(c => c.chapter)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.comment_id == commentId, ct);

        public Task<chapter_comment?> GetForOwnerAsync(Guid chapterId, Guid commentId, Guid readerId, CancellationToken ct = default)
            => _db.chapter_comments
                  .Include(c => c.reader).ThenInclude(r => r.account)
                  .Include(c => c.chapter)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.comment_id == commentId && c.reader_id == readerId, ct);

        public async Task AddAsync(chapter_comment comment, CancellationToken ct = default)
        {
            EnsureId(comment, nameof(chapter_comment.comment_id));
            _db.chapter_comments.Add(comment);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(chapter_comment comment, CancellationToken ct = default)
        {
            _db.chapter_comments.Update(comment);
            await _db.SaveChangesAsync(ct);
        }

    }
}

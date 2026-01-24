using System;
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
    public class ModerationRepository : BaseRepository, IModerationRepository
    {
        public ModerationRepository(AppDbContext db) : base(db)
        {
        }

        public Task<story?> GetStoryAsync(Guid storyId, CancellationToken ct = default)
            => _db.stories
                  .Include(s => s.author).ThenInclude(a => a.account)
                  .FirstOrDefaultAsync(s => s.story_id == storyId, ct);

        public async Task UpdateStoryAsync(story entity, CancellationToken ct = default)
        {
            _db.stories.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<chapter?> GetChapterAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapter
                  .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                  .Include(c => c.story).ThenInclude(s => s.language)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public async Task UpdateChapterAsync(chapter entity, CancellationToken ct = default)
        {
            _db.chapter.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<chapter_comment?> GetCommentAsync(Guid commentId, CancellationToken ct = default)
            => _db.chapter_comments
                  .Include(c => c.reader).ThenInclude(r => r.account)
                  .Include(c => c.chapter).ThenInclude(ch => ch.story)
                  .FirstOrDefaultAsync(c => c.comment_id == commentId, ct);

        public async Task UpdateCommentAsync(chapter_comment entity, CancellationToken ct = default)
        {
            _db.chapter_comments.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<report?> GetReportAsync(Guid reportId, CancellationToken ct = default)
            => _db.report.FirstOrDefaultAsync(r => r.report_id == reportId, ct);

        public async Task UpdateReportAsync(report entity, CancellationToken ct = default)
        {
            _db.report.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task BulkResolvePendingReportsAsync(string targetType, Guid targetId, Guid moderatorId, string note, CancellationToken ct = default)
        {
            var now = TimezoneConverter.VietnamNow;
            
            await _db.report
                .Where(r => r.target_type == targetType && r.target_id == targetId && r.status == "pending")
                .ExecuteUpdateAsync(r => r
                    .SetProperty(x => x.status, "resolved")
                    .SetProperty(x => x.moderator_id, moderatorId)
                    .SetProperty(x => x.reviewed_at, now), ct);
        }
    }
}

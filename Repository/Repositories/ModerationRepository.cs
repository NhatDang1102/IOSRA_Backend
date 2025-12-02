using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

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
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public async Task UpdateChapterAsync(chapter entity, CancellationToken ct = default)
        {
            _db.chapter.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<chapter_comment?> GetCommentAsync(Guid commentId, CancellationToken ct = default)
            => _db.chapter_comments
                  .Include(c => c.reader).ThenInclude(r => r.account)
                  .Include(c => c.chapter)
                  .FirstOrDefaultAsync(c => c.comment_id == commentId, ct);

        public async Task UpdateCommentAsync(chapter_comment entity, CancellationToken ct = default)
        {
            _db.chapter_comments.Update(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}

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
    public class StoryModerationRepository : BaseRepository, IStoryModerationRepository
    {
        public StoryModerationRepository(AppDbContext db) : base(db)
        {
        }

        public Task<List<story>> GetStoriesForModerationAsync(IEnumerable<string> statuses, CancellationToken ct = default)
        {
            var statusList = statuses?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray() ?? Array.Empty<string>();

            if (statusList.Length == 0)
            {
                return Task.FromResult(new List<story>());
            }

            return _db.stories
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.author).ThenInclude(a => a.rank)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Include(s => s.language)
                .Include(s => s.content_approves)
                .Where(s => statusList.Contains(s.status.ToLower()))
                .OrderByDescending(s => s.updated_at)
                .ToListAsync(ct);
        }

        public Task<content_approve?> GetContentApprovalByIdAsync(Guid reviewId, CancellationToken ct = default)
            => _db.content_approves
                  .Include(c => c.story!)
                      .ThenInclude(s => s.author!)
                      .ThenInclude(a => a.account)
                  .Include(c => c.story!)
                      .ThenInclude(s => s.author!)
                      .ThenInclude(a => a.rank)
                  .Include(c => c.story!)
                      .ThenInclude(s => s.language)
                  .Include(c => c.story!)
                      .ThenInclude(s => s.story_tags)
                      .ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(c => c.review_id == reviewId, ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}


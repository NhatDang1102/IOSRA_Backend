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
    public class ChapterModerationRepository : BaseRepository, IChapterModerationRepository
    {
        public ChapterModerationRepository(AppDbContext db) : base(db)
        {
        }

        //lấy list theo status query cho cmod
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

            return await _db.chapter
                .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                .Include(c => c.story).ThenInclude(s => s.language)
                .Include(c => c.content_approves)
                .Where(c => statusList.Contains(c.status.ToLower()))
                .OrderByDescending(c => c.submitted_at ?? c.updated_at)
                .ToListAsync(ct);
        }
        //như trên nhưng lấy theo id 
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
                      .ThenInclude(ch => ch.story!)
                      .ThenInclude(s => s.language)
                  .FirstOrDefaultAsync(c => c.review_id == reviewId, ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}


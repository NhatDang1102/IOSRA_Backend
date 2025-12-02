using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class ContentModRepository : BaseRepository, IContentModRepository
    {
        public ContentModRepository(AppDbContext db) : base(db)
        {
        }

        public Task IncrementStoryDecisionAsync(Guid moderatorAccountId, bool approved, CancellationToken ct = default)
            => _db.ContentMods
                .Where(m => m.account_id == moderatorAccountId)
                .ExecuteUpdateAsync(setters => approved
                    ? setters.SetProperty(m => m.total_approved_stories, m => m.total_approved_stories + 1)
                    : setters.SetProperty(m => m.total_rejected_stories, m => m.total_rejected_stories + 1), ct);

        public Task IncrementChapterDecisionAsync(Guid moderatorAccountId, bool approved, CancellationToken ct = default)
            => _db.ContentMods
                .Where(m => m.account_id == moderatorAccountId)
                .ExecuteUpdateAsync(setters => approved
                    ? setters.SetProperty(m => m.total_approved_chapters, m => m.total_approved_chapters + 1)
                    : setters.SetProperty(m => m.total_rejected_chapters, m => m.total_rejected_chapters + 1), ct);

        public Task IncrementReportHandledAsync(Guid moderatorAccountId, CancellationToken ct = default)
            => _db.ContentMods
                .Where(m => m.account_id == moderatorAccountId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.total_reported_handled, m => m.total_reported_handled + 1), ct);
    }
}

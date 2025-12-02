using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DataModels;
using Repository.DBContext;
using Repository.Interfaces;
using Repository.Utils;

namespace Repository.Repositories
{
    public class ContentModStatRepository : BaseRepository, IContentModStatRepository
    {
        public ContentModStatRepository(AppDbContext db) : base(db)
        {
        }

        public Task<List<StatPointData>> GetPublishedStoriesAsync(DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.stories
                .AsNoTracking()
                .Where(s => s.status == "published"
                            && s.published_at != null
                            && s.published_at >= from
                            && s.published_at <= to)
                .Select(s => new StatPointSource
                {
                    Timestamp = s.published_at!.Value,
                    Value = 1
                });

            return GroupAsync(query, period, ct);
        }

        public Task<List<StatPointData>> GetPublishedChaptersAsync(DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.chapter
                .AsNoTracking()
                .Where(c => c.status == "published"
                            && c.published_at != null
                            && c.published_at >= from
                            && c.published_at <= to)
                .Select(c => new StatPointSource
                {
                    Timestamp = c.published_at!.Value,
                    Value = 1
                });

            return GroupAsync(query, period, ct);
        }

        public Task<List<StatPointData>> GetStoryDecisionStatsAsync(string status, DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.content_approves
                .AsNoTracking()
                .Where(a => a.approve_type == "story"
                            && a.created_at >= from
                            && a.created_at <= to);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.status == status);
            }

            var source = query.Select(a => new StatPointSource
            {
                Timestamp = a.created_at,
                Value = 1
            });

            return GroupAsync(source, period, ct);
        }

        public Task<List<StatPointData>> GetReportStatsAsync(string status, DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.report
                .AsNoTracking()
                .Where(r => r.created_at >= from && r.created_at <= to);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.status == status);
            }

            var source = query.Select(r => new StatPointSource
            {
                Timestamp = r.created_at,
                Value = 1
            });

            return GroupAsync(source, period, ct);
        }

        public Task<List<StatPointData>> GetHandledReportsAsync(Guid moderatorAccountId, string status, DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.report
                .AsNoTracking()
                .Where(r => r.moderator_id == moderatorAccountId
                            && r.reviewed_at != null
                            && r.reviewed_at >= from
                            && r.reviewed_at <= to);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.status == status);
            }
            else
            {
                query = query.Where(r => r.status != "pending");
            }

            var source = query.Select(r => new StatPointSource
            {
                Timestamp = r.reviewed_at ?? r.created_at,
                Value = 1
            });

            return GroupAsync(source, period, ct);
        }

        private async Task<List<StatPointData>> GroupAsync(IQueryable<StatPointSource> source, string period, CancellationToken ct)
        {
            var data = await source.ToListAsync(ct);
            return data
                .GroupBy(x => StatPeriodHelper.GetPeriodStart(x.Timestamp, period))
                .Select(g =>
                {
                    var start = g.Key;
                    var end = StatPeriodHelper.GetPeriodEnd(start, period);
                    return new StatPointData
                    {
                        Label = StatPeriodHelper.BuildLabel(start, end, period),
                        RangeStart = start,
                        RangeEnd = end,
                        Value = g.Sum(v => v.Value)
                    };
                })
                .OrderBy(g => g.RangeStart)
                .ToList();
        }

        private sealed class StatPointSource
        {
            public DateTime Timestamp { get; set; }
            public long Value { get; set; }
        }
    }
}

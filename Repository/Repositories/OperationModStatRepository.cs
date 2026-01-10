using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.OperationMod;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DataModels;
using Repository.DBContext;
using Repository.Interfaces;
using Repository.Utils;

namespace Repository.Repositories
{
    public class OperationModStatRepository : BaseRepository, IOperationModStatRepository
    {
        public OperationModStatRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<OperationRevenueData> GetRevenueAsync(DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var receipts = _db.payment_receipts
                .AsNoTracking()
                .Where(r => r.created_at >= from && r.created_at <= to);

            long dia = await receipts.Where(r => r.type == "dia_topup").SumAsync(r => (long)r.amount_vnd, ct);
            long subscription = await receipts.Where(r => r.type == "subscription").SumAsync(r => (long)r.amount_vnd, ct);
            long voice = await receipts.Where(r => r.type == "voice_topup").SumAsync(r => (long)r.amount_vnd, ct);

            var source = receipts.Select(r => new StatPointSource
            {
                Timestamp = r.created_at,
                Value = (long)r.amount_vnd
            });

            return new OperationRevenueData
            {
                DiaTopup = dia,
                Subscription = subscription,
                VoiceTopup = voice,
                Points = await GroupAsync(source, period, ct)
            };
        }

        public Task<List<StatPointData>> GetRequestStatsAsync(string requestType, DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            var query = _db.op_request
                .AsNoTracking()
                .Where(r => r.request_type == requestType
                            && r.created_at >= from
                            && r.created_at <= to)
                .Select(r => new StatPointSource
                {
                    Timestamp = r.created_at,
                    Value = 1
                });

            return GroupAsync(query, period, ct);
        }

        public Task<List<StatPointData>> GetAuthorRevenueStatsAsync(string metric, DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            IQueryable<StatPointSource> source;

            if (metric == "withdrawn")
            {
                source = _db.op_request
                    .AsNoTracking()
                    .Where(r => r.request_type == "withdraw"
                                && r.status == "approved"
                                && r.reviewed_at >= from
                                && r.reviewed_at <= to
                                && r.withdraw_amount.HasValue)
                    .Select(r => new StatPointSource
                    {
                        Timestamp = r.reviewed_at ?? r.created_at,
                        Value = (long)r.withdraw_amount!.Value
                    });
            }
            else
            {
                var purchases = _db.author_revenue_transaction
                    .AsNoTracking()
                    .Where(t => t.created_at >= from && t.created_at <= to && t.type == "purchase");

                source = purchases.Select(t => new StatPointSource
                {
                    Timestamp = t.created_at,
                    Value = t.amount
                });
            }

            return GroupAsync(source, period, ct);
        }

        public async Task<UserGrowthStatsResponse> GetUserGrowthAsync(DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            // Fetch raw data first to handle grouping in memory if necessary, 
            // but for performance, we should project minimally.
            // Joining account_role is tricky with EF Core GroupBy.
            // Simplified approach: Get all accounts in range with their roles.
            
            var accounts = await _db.accounts
                .AsNoTracking()
                .Where(a => a.created_at >= from && a.created_at <= to)
                .Select(a => new 
                { 
                    a.created_at, 
                    IsAuthor = a.account_roles.Any(ar => ar.role.role_code == "author")
                })
                .ToListAsync(ct);

            var grouped = accounts
                .GroupBy(x => StatPeriodHelper.GetPeriodStart(x.created_at, period))
                .Select(g => new UserGrowthPoint
                {
                    Date = g.Key,
                    NewReaders = g.Count(x => !x.IsAuthor),
                    NewAuthors = g.Count(x => x.IsAuthor),
                    TotalNew = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            return new UserGrowthStatsResponse { Data = grouped };
        }

        public async Task<List<TrendingStoryResponse>> GetTrendingStoriesAsync(DateTime from, DateTime to, int limit, CancellationToken ct = default)
        {
            // Sum view counts from weekly views table
            var query = _db.story_weekly_view
                .AsNoTracking()
                .Where(v => v.week_start_utc >= from && v.week_start_utc <= to)
                .GroupBy(v => v.story_id)
                .Select(g => new 
                {
                    StoryId = g.Key,
                    TotalViews = g.Sum(v => (long)v.view_count)
                })
                .OrderByDescending(x => x.TotalViews)
                .Take(limit);

            // Fetch details (Title, Author)
            // Need to join back to stories table
            var result = await query.Join(_db.stories,
                stat => stat.StoryId,
                story => story.story_id,
                (stat, story) => new TrendingStoryResponse
                {
                    StoryId = stat.StoryId,
                    Title = story.title,
                    CoverUrl = story.cover_url,
                    AuthorName = story.author.account.username, // Nav property chain
                    TotalViewsInPeriod = stat.TotalViews
                })
                .ToListAsync(ct);

            return result;
        }

        public async Task<SystemEngagementResponse> GetSystemEngagementAsync(DateTime from, DateTime to, string period, CancellationToken ct = default)
        {
            // 1. Views (from weekly views)
            var totalViews = await _db.story_weekly_view
                .AsNoTracking()
                .Where(v => v.week_start_utc >= from && v.week_start_utc <= to)
                .SumAsync(v => (long)v.view_count, ct);

            // 2. Comments (time series)
            var comments = await _db.chapter_comments
                .AsNoTracking()
                .Where(c => c.created_at >= from && c.created_at <= to)
                .Select(c => c.created_at)
                .ToListAsync(ct);

            // 3. Follows (time series)
            var follows = await _db.follows
                .AsNoTracking()
                .Where(f => f.created_at >= from && f.created_at <= to)
                .Select(f => f.created_at)
                .ToListAsync(ct);

            var totalComments = comments.Count;
            var totalFollows = follows.Count;

            // Group for chart (by period)
            // Union dates from comments and follows to get comprehensive timeline
            var commentGroups = comments.GroupBy(d => StatPeriodHelper.GetPeriodStart(d, period))
                .ToDictionary(g => g.Key, g => g.Count());
                
            var followGroups = follows.GroupBy(d => StatPeriodHelper.GetPeriodStart(d, period))
                .ToDictionary(g => g.Key, g => g.Count());

            var allDates = commentGroups.Keys.Union(followGroups.Keys).OrderBy(d => d).ToList();
            
            var chartData = allDates.Select(date => new EngagementPoint
            {
                Date = date,
                NewComments = commentGroups.ContainsKey(date) ? commentGroups[date] : 0,
                NewFollows = followGroups.ContainsKey(date) ? followGroups[date] : 0
            }).ToList();

            return new SystemEngagementResponse
            {
                TotalViews = totalViews,
                TotalNewComments = totalComments,
                TotalNewFollows = totalFollows,
                ChartData = chartData
            };
        }

        public async Task<List<TagTrendResponse>> GetTagTrendsAsync(DateTime from, DateTime to, int limit, CancellationToken ct = default)
        {
            var query = _db.story_weekly_view
                .AsNoTracking()
                .Where(wv => wv.week_start_utc >= from && wv.week_start_utc <= to)
                .Join(_db.story_tag, 
                      wv => wv.story_id, 
                      st => st.story_id, 
                      (wv, st) => new { wv, st })
                .Join(_db.tag, 
                      combined => combined.st.tag_id, 
                      t => t.tag_id, 
                      (combined, t) => new { View = combined.wv, Tag = t })
                .GroupBy(x => new { x.Tag.tag_id, x.Tag.tag_name })
                .Select(g => new TagTrendResponse
                {
                    TagId = g.Key.tag_id,
                    TagName = g.Key.tag_name,
                    TotalViews = g.Sum(x => (long)x.View.view_count),
                    StoryCount = g.Select(x => x.View.story_id).Distinct().Count()
                });

            return await query
                .OrderByDescending(x => x.TotalViews)
                .Take(limit)
                .ToListAsync(ct);
        }

        public async Task IncrementReportGeneratedCountAsync(Guid userId, CancellationToken ct = default)
        {
            var omod = await _db.OperationMods.FirstOrDefaultAsync(o => o.account_id == userId, ct);
            if (omod != null)
            {
                omod.reports_generated++;
                await _db.SaveChangesAsync(ct);
            }
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

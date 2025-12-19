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
    public class StoryCatalogRepository : BaseRepository, IStoryCatalogRepository
    {
        private static readonly string[] PublicStoryStatuses = { "published", "completed" };

        public StoryCatalogRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<(List<story> Items, int Total)> SearchPublishedStoriesAsync(string? query, Guid? tagId, Guid? authorId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var storiesQuery = _db.stories
                .AsNoTracking()
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Where(s => PublicStoryStatuses.Contains(s.status))
                .Where(s => s.chapters.Any(c => c.status == "published"));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                var likePattern = BuildLikePattern(term);
                const string escapeChar = "\\";

                storiesQuery = storiesQuery.Where(s =>
                    EF.Functions.Like(s.title, likePattern, escapeChar) ||
                    (s.desc != null && EF.Functions.Like(s.desc, likePattern, escapeChar)));
            }

            if (tagId.HasValue && tagId.Value != Guid.Empty)
            {
                storiesQuery = storiesQuery.Where(s => s.story_tags.Any(st => st.tag_id == tagId.Value));
            }

            if (authorId.HasValue && authorId.Value != Guid.Empty)
            {
                storiesQuery = storiesQuery.Where(s => s.author_id == authorId.Value);
            }

            var total = await storiesQuery.CountAsync(ct);
            var skip = (page - 1) * pageSize;

            var items = await storiesQuery
                .OrderByDescending(s => s.published_at ?? s.updated_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<List<story>> GetLatestPublishedStoriesAsync(int limit, CancellationToken ct = default)
        {
            if (limit <= 0)
            {
                return new List<story>();
            }

            return await _db.stories
                .AsNoTracking()
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Where(s => PublicStoryStatuses.Contains(s.status))
                .Where(s => s.chapters.Any(c => c.status == "published"))
                .OrderByDescending(s => s.published_at ?? s.updated_at)
                .Take(limit)
                .ToListAsync(ct);
        }

        public async Task<List<story>> GetStoriesByIdsAsync(IEnumerable<Guid> storyIds, CancellationToken ct = default)
        {
            var ids = storyIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
            if (ids.Length == 0)
            {
                return new List<story>();
            }

            return await _db.stories
                .AsNoTracking()
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Where(s => ids.Contains(s.story_id) && PublicStoryStatuses.Contains(s.status))
                .ToListAsync(ct);
        }

        public Task<story?> GetPublishedStoryByIdAsync(Guid storyId, CancellationToken ct = default)
            => _db.stories
                  .AsNoTracking()
                  .Include(s => s.author).ThenInclude(a => a.account)
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(s => s.story_id == storyId && PublicStoryStatuses.Contains(s.status), ct);
        private static string BuildLikePattern(string term)
        {
            var escaped = term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            return $"%{escaped}%";
        }

        public async Task<(List<story> Items, int Total)> SearchPublishedStoriesAdvancedAsync(string? query, Guid? tagId, Guid? authorId, bool? isPremium, double? minAvgRating, string? sortBy, bool sortDesc, DateTime? weekStartUtc, int page, int pageSize, CancellationToken ct = default)
        {
            // base query: chỉ lấy story public
            var q = _db.stories
                .AsNoTracking()
                .Include(s => s.author).ThenInclude(a => a.account)
                .Where(s => PublicStoryStatuses.Contains(s.status))
                .Where(s => s.chapters.Any(c => c.status == "published"));

            // text search
            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                var likePattern = BuildLikePattern(term);
                const string escapeChar = "\\";

                q = q.Where(s =>
                    EF.Functions.Like(s.title, likePattern, escapeChar) ||
                    (s.desc != null && EF.Functions.Like(s.desc, likePattern, escapeChar)) ||
                    EF.Functions.Like(s.author.account.username, likePattern, escapeChar)
                );
            }

            // filter tag
            if (tagId.HasValue)
            {
                q = q.Where(s => s.story_tags.Any(t => t.tag_id == tagId.Value));
            }

            // filter author
            if (authorId.HasValue)
            {
                q = q.Where(s => s.author_id == authorId.Value);
            }

            // filter premium
            if (isPremium.HasValue)
            {
                q = q.Where(s => s.is_premium == isPremium.Value);
            }


            // subquery: aggregate rating (avg)
            var ratingAgg = _db.story_ratings
                .GroupBy(r => r.story_id)
                .Select(g => new
                {
                    story_id = g.Key,
                    avg = g.Average(x => (double?)x.score),
                });

            if (minAvgRating.HasValue)
            {
                var min = minAvgRating.Value;

                var joined =
                    from s in q
                    join ra in ratingAgg on s.story_id equals ra.story_id into gj
                    from ra in gj.DefaultIfEmpty()
                    select new { s, avg = (ra.avg ?? 0.0) };   

                q = joined
                    .Where(x => x.avg >= min)
                    .Select(x => x.s);
            }

            // subquery: weekly views (cho sort WeeklyViews)
            IQueryable<dynamic> viewAgg = Enumerable.Empty<dynamic>().AsQueryable();
            if (string.Equals(sortBy, "weeklyViews", StringComparison.OrdinalIgnoreCase))
            {
                if (!weekStartUtc.HasValue)
                {
                    // fallback: dùng đúng giá trị trong DB nếu caller không truyền
                    weekStartUtc = DateTime.UtcNow.Date;
                }

                var wk = weekStartUtc.Value;
                viewAgg = _db.story_weekly_view
                    .Where(v => v.week_start_utc == wk)
                    .Select(v => new { v.story_id, views = (long)v.view_count });
            }

            // sort
            if (string.Equals(sortBy, "weeklyViews", StringComparison.OrdinalIgnoreCase))
            {
                if (!weekStartUtc.HasValue)
                    weekStartUtc = DateTime.UtcNow.Date;

                var w = weekStartUtc.Value;
                var wk = new DateTime(w.Year, w.Month, w.Day, 0, 0, 0, DateTimeKind.Utc);

                var joined =
                    from s in q
                    join vw in _db.story_weekly_view.Where(v => v.week_start_utc == wk)
                        on s.story_id equals vw.story_id into gj
                    from vw in gj.DefaultIfEmpty()
                    select new { s, views = (vw == null ? (decimal)0 : (decimal)vw.view_count) };

                q = (sortDesc
                    ? joined.OrderByDescending(x => x.views)
                    : joined.OrderBy(x => x.views))
                    .Select(x => x.s);
            }
            else if (string.Equals(sortBy, "topRated", StringComparison.OrdinalIgnoreCase))
            {
                var rated = q.Select(s => new
                {
                    s,
                    avg = _db.story_ratings
                  .Where(r => r.story_id == s.story_id)
                  .Select(r => (double?)r.score)   
                  .Average() ?? 0.0               
                });

                q = (sortDesc
                    ? rated.OrderByDescending(x => x.avg)
                    : rated.OrderBy(x => x.avg))
                    .Select(x => x.s);
            }
            else if (string.Equals(sortBy, "mostChapters", StringComparison.OrdinalIgnoreCase))
            {
                q = (sortDesc
                    ? q.OrderByDescending(s => s.chapters.Count(c => c.status == "published"))
                    : q.OrderBy(s => s.chapters.Count(c => c.status == "published")));
            }
            else
            {
                q = (sortDesc
                    ? q.OrderByDescending(s => s.published_at)
                    : q.OrderBy(s => s.published_at));
            }

            // total
            var total = await q.CountAsync(ct);

            // page
            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task IncrementTotalViewsAsync(IReadOnlyDictionary<Guid, ulong> viewIncrements, CancellationToken ct = default)
        {
            if (viewIncrements == null || viewIncrements.Count == 0)
            {
                return;
            }

            var storyIds = viewIncrements.Keys.ToList();

            
            foreach (var storyId in storyIds)
            {
                if (viewIncrements.TryGetValue(storyId, out var increment) && increment > 0)
                {
                    await _db.stories
                        .Where(s => s.story_id == storyId)
                        .ExecuteUpdateAsync(p => p.SetProperty(s => s.total_views, s => s.total_views + (long)increment), ct);
                }
            }
        }
    }
}

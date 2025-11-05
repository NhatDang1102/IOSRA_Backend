using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class StoryWeeklyViewRepository : BaseRepository, IStoryWeeklyViewRepository
    {
        public StoryWeeklyViewRepository(AppDbContext db) : base(db)
        {
        }

        public async Task UpsertWeeklyViewsAsync(DateTime weekStartUtc, IReadOnlyCollection<StoryViewCount> items, CancellationToken ct = default)
        {
            if (items.Count == 0)
            {
                return;
            }

            var normalizedWeekStart = weekStartUtc.TrimToMinute();
            var storyIds = items.Select(i => i.StoryId).ToArray();

            var existing = await _db.story_weekly_views
                .Where(x => x.week_start_utc == normalizedWeekStart && storyIds.Contains(x.story_id))
                .ToListAsync(ct);

            var existingLookup = existing.ToDictionary(x => x.story_id, x => x);
            var now = DateTime.UtcNow;

            foreach (var item in items)
            {
                if (existingLookup.TryGetValue(item.StoryId, out var entity))
                {
                    entity.view_count = item.ViewCount;
                    entity.captured_at_utc = now;
                }
                else
                {
                    _db.story_weekly_views.Add(new story_weekly_view
                    {
                        story_weekly_view_id = NewId(),
                        story_id = item.StoryId,
                        week_start_utc = normalizedWeekStart,
                        view_count = item.ViewCount,
                        captured_at_utc = now
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<StoryViewCount>> GetTopWeeklyViewsAsync(DateTime weekStartUtc, int limit, CancellationToken ct = default)
        {
            var normalizedWeekStart = weekStartUtc.TrimToMinute();

            var data = await _db.story_weekly_views
                .Where(x => x.week_start_utc == normalizedWeekStart)
                .OrderByDescending(x => x.view_count)
                .ThenBy(x => x.story_id)
                .Take(limit)
                .Select(x => new StoryViewCount
                {
                    StoryId = x.story_id,
                    ViewCount = x.view_count
                })
                .ToListAsync(ct);

            return data;
        }

        public Task<bool> HasWeekSnapshotAsync(DateTime weekStartUtc, CancellationToken ct = default)
        {
            var normalizedWeekStart = weekStartUtc.TrimToMinute();
            return _db.story_weekly_views.AnyAsync(x => x.week_start_utc == normalizedWeekStart, ct);
        }
    }

    internal static class DateTimeExtensions
    {
        public static DateTime TrimToMinute(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, DateTimeKind.Utc);
        }
    }
}

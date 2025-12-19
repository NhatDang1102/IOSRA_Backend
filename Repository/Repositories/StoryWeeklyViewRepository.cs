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
using Repository.Utils;

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
            //chuẩn hóa tg bắt đầu tuần
            var normalizedWeekStart = weekStartUtc.TrimToMinute();

            //tạo 1 mảng tất cả story có lượt view mới mà background bóc ra
            var storyIds = items.Select(i => i.StoryId).ToArray();
            //bóc hết story id của tuần hiện tại trong db ra để coi cái nào cần update lượt view
            var existing = await _db.story_weekly_view
                .Where(x => x.week_start_utc == normalizedWeekStart && storyIds.Contains(x.story_id))
                .ToListAsync(ct);
            //chuyển existing ở trên thành dictonary để dò cho lẹ (O(1) thay vì O(N) dò hết list mỗi lần upsert
            var existingLookup = existing.ToDictionary(x => x.story_id, x => x);
            var now = TimezoneConverter.VietnamNow;

            //key nào tìm đc trong dictionary thì mới update, néu ko thì insert mới vô 
            foreach (var item in items)
            {
                if (existingLookup.TryGetValue(item.StoryId, out var entity))
                {
                    entity.view_count = item.ViewCount;
                    entity.captured_at = now;
                }
                else
                {
                    _db.story_weekly_view.Add(new story_weekly_view
                    {
                        story_weekly_view_id = NewId(),
                        story_id = item.StoryId,
                        week_start_utc = normalizedWeekStart,
                        view_count = item.ViewCount,
                        captured_at = now
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<StoryViewCount>> GetTopWeeklyViewsAsync(DateTime weekStartUtc, int limit, CancellationToken ct = default)
        {
            var normalizedWeekStart = weekStartUtc.TrimToMinute();

            var data = await _db.story_weekly_view
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

        public async Task<IReadOnlyList<StoryViewCount>> GetWeeklyViewsByStoryIdsAsync(DateTime weekStartUtc, IEnumerable<Guid> storyIds, CancellationToken ct = default)
        {
            var normalizedWeekStart = weekStartUtc.TrimToMinute();
            var ids = storyIds.ToArray();

            return await _db.story_weekly_view
                .Where(x => x.week_start_utc == normalizedWeekStart && ids.Contains(x.story_id))
                .Select(x => new StoryViewCount { StoryId = x.story_id, ViewCount = x.view_count })
                .ToListAsync(ct);
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

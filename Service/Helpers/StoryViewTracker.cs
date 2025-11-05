using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Service.Interfaces;
using StackExchange.Redis;

namespace Service.Helpers
{
    public class StoryViewTracker : IStoryViewTracker
    {
        private static readonly TimeSpan DebounceTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan WeeklyKeyTtl = TimeSpan.FromDays(21);

        private readonly IConnectionMultiplexer _redis;

        public StoryViewTracker(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task RecordViewAsync(Guid storyId, Guid? viewerAccountId, string? viewerFingerprint, CancellationToken ct = default)
        {
            var database = _redis.GetDatabase();
            var viewerKey = ResolveViewerKey(viewerAccountId, viewerFingerprint);

            if (viewerKey is not null)
            {
                var dedupeKey = $"story:view:dedupe:{storyId:N}:{viewerKey}";
                var added = await database.StringSetAsync(dedupeKey, "1", DebounceTtl, When.NotExists);
                if (!added)
                {
                    return;
                }
            }

            var weekStart = StoryViewTimeHelper.GetCurrentWeekStartUtc();
            var weekKey = GetWeeklyKey(weekStart);

            await database.SortedSetIncrementAsync(weekKey, storyId.ToString("N"), 1);
            await database.KeyExpireAsync(weekKey, WeeklyKeyTtl);
        }

        public async Task<IReadOnlyList<StoryViewCount>> GetWeeklyTopAsync(DateTime weekStartUtc, int limit, CancellationToken ct = default)
        {
            var database = _redis.GetDatabase();
            var key = GetWeeklyKey(StoryViewTimeHelper.NormalizeToMinuteUtc(weekStartUtc));

            var results = await database.SortedSetRangeByRankWithScoresAsync(key, 0, limit - 1, Order.Descending);

            return results
                .Select(entry => new StoryViewCount
                {
                    StoryId = Guid.TryParse(entry.Element, out var id) ? id : Guid.Empty,
                    ViewCount = (ulong)Math.Max(0, entry.Score)
                })
                .Where(x => x.StoryId != Guid.Empty)
                .ToList();
        }

        public DateTime GetCurrentWeekStartUtc()
        {
            return StoryViewTimeHelper.GetCurrentWeekStartUtc();
        }

        public async Task<IReadOnlyList<StoryViewCount>> GetWeeklyViewsAsync(DateTime weekStartUtc, CancellationToken ct = default)
        {
            var database = _redis.GetDatabase();
            var key = GetWeeklyKey(StoryViewTimeHelper.NormalizeToMinuteUtc(weekStartUtc));

            var results = await database.SortedSetRangeByRankWithScoresAsync(key, 0, -1, Order.Descending);

            return results
                .Select(entry => new StoryViewCount
                {
                    StoryId = Guid.TryParse(entry.Element, out var id) ? id : Guid.Empty,
                    ViewCount = (ulong)Math.Max(0, entry.Score)
                })
                .Where(x => x.StoryId != Guid.Empty)
                .ToList();
        }

        private static string GetWeeklyKey(DateTime weekStartUtc)
        {
            return $"story:views:week:{weekStartUtc:yyyyMMddHHmm}";
        }

        private static string? ResolveViewerKey(Guid? accountId, string? fingerprint)
        {
            if (accountId.HasValue && accountId.Value != Guid.Empty)
            {
                return $"acc:{accountId.Value:N}";
            }

            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                return $"fp:{fingerprint.Trim()}";
            }

            return null;
        }
    }
}

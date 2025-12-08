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
        //cách nhau 5 phút cho mỗi user/device để tính 1 view
        private static readonly TimeSpan DebounceTtl = TimeSpan.FromMinutes(5);

        //key redis sống 21 ngày 
        private static readonly TimeSpan WeeklyKeyTtl = TimeSpan.FromDays(21);

        private readonly IConnectionMultiplexer _redis;

        public StoryViewTracker(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }
        //method ghi nhận view mới
        public async Task RecordViewAsync(Guid storyId, Guid? viewerAccountId, string? viewerFingerprint, CancellationToken ct = default)
        {
            //kết nối db redis
            var database = _redis.GetDatabase();
            //xác định key độc nhất cho người xem (lấy từ id tài khoản, nếu ko có thì sang fingerprint device)
            var viewerKey = ResolveViewerKey(viewerAccountId, viewerFingerprint);

            if (viewerKey is not null)
            {
                //tạo key story id + viewerkey để đảm bảo k bị dupe 
                var dedupeKey = $"story:view:dedupe:{storyId:N}:{viewerKey}";
                //đặt thử key vừa tạo vô redis để chắc chắn là ko bị dupe
                var added = await database.StringSetAsync(dedupeKey, "1", DebounceTtl, When.NotExists);
                if (!added)
                {
                    return;
                }
            }
            //lấy thời điểm bắt đầu tuần 
            var weekStart = StoryViewTimeHelper.GetCurrentWeekStartUtc();
            //tạo key format: story:views:week:YYYYMMDDHHMM
            var weekKey = GetWeeklyKey(weekStart);
            //+1 score view cho storyId đó trong sorted set lên 1
            await database.SortedSetIncrementAsync(weekKey, storyId.ToString("N"), 1);
            //set time hết hạn cho key 
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
        { //bóc key từ redis dể truy vấn sorted set (lấy hết, cái trên giống nhưng lấy theo limit)
            var database = _redis.GetDatabase();
            var key = GetWeeklyKey(StoryViewTimeHelper.NormalizeToMinuteUtc(weekStartUtc));
            //descending
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
        { //nếu ko có account thì láy fingerprint device
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

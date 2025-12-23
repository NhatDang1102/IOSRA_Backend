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
    // Lớp xử lý ghi nhận lượt xem (View Tracking) sử dụng Redis để đạt hiệu suất cao
    // Cơ chế: Sử dụng Sorted Set để lưu view theo tuần (phục vụ Top Weekly) và StringSet để Deduplication (chống spam view)
    public class StoryViewTracker : IStoryViewTracker
    {
        // Mỗi User/Thiết bị chỉ được tính 1 view cho 1 truyện trong mỗi 5 phút (Debounce)
        private static readonly TimeSpan DebounceTtl = TimeSpan.FromMinutes(5);

        // Giữ dữ liệu view tuần trong Redis 21 ngày trước khi tự động xóa
        private static readonly TimeSpan WeeklyKeyTtl = TimeSpan.FromDays(21);

        private readonly IConnectionMultiplexer _redis;

        public StoryViewTracker(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        // Ghi nhận lượt xem mới
        // Flow:
        // 1. Xác định định danh người xem (Account ID hoặc IP Fingerprint).
        // 2. Kiểm tra trong Redis xem User này đã xem truyện này trong 5 phút gần đây chưa (Dedupe).
        // 3. Nếu chưa -> Tăng điểm Score trong Sorted Set cho StoryId đó theo tuần hiện tại.
        public async Task RecordViewAsync(Guid storyId, Guid? viewerAccountId, string? viewerFingerprint, CancellationToken ct = default)
        {
            var database = _redis.GetDatabase();
            
            // Lấy định danh người xem (Ưu tiên Account ID > IP Fingerprint)
            var viewerKey = ResolveViewerKey(viewerAccountId, viewerFingerprint);

            if (viewerKey is not null)
            {
                // Key chống spam: story:view:dedupe:{StoryId}:{ViewerId}
                var dedupeKey = $"story:view:dedupe:{storyId:N}:{viewerKey}";
                
                // Đặt key vào Redis với thời gian sống (TTL) 5 phút. 
                // Nếu key đã tồn tại (StringSetAsync trả về false), nghĩa là user vừa xem xong -> Không tính thêm view.
                var added = await database.StringSetAsync(dedupeKey, "1", DebounceTtl, When.NotExists);
                if (!added)
                {
                    return;
                }
            }

            // Lấy thời điểm bắt đầu của tuần hiện tại (UTC) để xác định Key tuần
            var weekStart = StoryViewTimeHelper.GetCurrentWeekStartUtc();
            var weekKey = GetWeeklyKey(weekStart);
            
            // Tăng lượt xem cho truyện (Dùng Sorted Set - ZINCRBY)
            // Cấu trúc: Key="story:views:week:YYYYMMDD", Member=StoryId, Score=Lượt xem
            await database.SortedSetIncrementAsync(weekKey, storyId.ToString("N"), 1);
            
            // Đặt thời gian hết hạn cho Key tuần để Redis tự giải phóng bộ nhớ
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

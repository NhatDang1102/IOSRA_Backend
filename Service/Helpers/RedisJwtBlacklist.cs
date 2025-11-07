using Service.Interfaces;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

using Repository.Utils;

namespace Service.Helpers
{
    // Service quản lý blacklist JWT token khi logout (sử dụng Redis)
    public class RedisJwtBlacklist : IJwtBlacklistService
    {
        private readonly IConnectionMultiplexer _redis;
        private const string Prefix = "jwt:blacklist"; // Prefix cho Redis keys
        public RedisJwtBlacklist(IConnectionMultiplexer redis) => _redis = redis;

        // Thêm JWT ID vào blacklist với TTL bằng thời gian còn lại của token
        public async Task BlacklistAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();
            // Tính TTL = thời gian còn lại đến khi token hết hạn
            var localNow = new DateTimeOffset(TimezoneConverter.VietnamNow, TimezoneConverter.VietnamOffset);

            var expiresLocal = expiresAtUtc.ToOffset(TimezoneConverter.VietnamOffset);

            var ttl = expiresLocal - localNow;
            if (ttl < TimeSpan.Zero) ttl = TimeSpan.FromMinutes(1); // Tối thiểu 1 phút
            // Lưu vào Redis với TTL - sau khi hết hạn key tự động bị xóa
            await db.StringSetAsync($"{Prefix}:{jti}", "1", ttl);
        }

        // Kiểm tra xem JWT ID có trong blacklist không
        public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync($"{Prefix}:{jti}");
        }
    }
}

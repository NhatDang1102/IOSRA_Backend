using System;
using System.Threading;
using System.Threading.Tasks;
using Repository.Utils;
using Service.Interfaces;
using StackExchange.Redis;

namespace Service.Helpers
{

    public class RedisJwtBlacklist : IJwtBlacklistService
    {
        private const string Prefix = "jwt:blacklist";

        private readonly IConnectionMultiplexer _redis;

        public RedisJwtBlacklist(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        public async Task BlacklistAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();

            // TTL should be calculated using UTC to avoid offset issues.
            var nowUtc = DateTimeOffset.UtcNow;
            var ttl = expiresAtUtc - nowUtc;
            if (ttl < TimeSpan.Zero)
            {
                ttl = TimeSpan.FromMinutes(1);
            }

            await db.StringSetAsync($"{Prefix}:{jti}", "1", ttl);
        }

        public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync($"{Prefix}:{jti}");
        }
    }
}

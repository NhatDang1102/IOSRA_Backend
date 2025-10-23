using Service.Interfaces;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public class RedisJwtBlacklist : IJwtBlacklistService
    {
        private readonly IConnectionMultiplexer _redis;
        private const string Prefix = "jwt:blacklist";
        public RedisJwtBlacklist(IConnectionMultiplexer redis) => _redis = redis;

        public async Task BlacklistAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();
            var ttl = expiresAtUtc - DateTimeOffset.UtcNow;
            if (ttl < TimeSpan.Zero) ttl = TimeSpan.FromMinutes(1);
            await db.StringSetAsync($"{Prefix}:{jti}", "1", ttl);
        }

        public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default)
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync($"{Prefix}:{jti}");
        }
    }
}

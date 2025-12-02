using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Repository.Utils;
using Service.Interfaces;
using Service.Models;
using StackExchange.Redis;

namespace Service.Helpers
{
    public class RedisRefreshTokenStore : IRefreshTokenStore
    {
        private const string KeyPrefix = "refresh";
        private readonly IDatabase _db;
        private readonly TimeSpan _lifetime;

        public RedisRefreshTokenStore(IConnectionMultiplexer redis, IConfiguration configuration)
        {
            _db = redis.GetDatabase();
            var days = int.TryParse(configuration["Jwt:RefreshTokenDays"], out var d) ? d : 14;
            _lifetime = TimeSpan.FromDays(days <= 0 ? 14 : days);
        }

        public async Task<RefreshTokenIssueResult> IssueAsync(Guid accountId, CancellationToken ct = default)
        {
            var tokenId = Guid.NewGuid();
            var token = Encode(accountId, tokenId);
            var key = BuildKey(tokenId);

            var vietnamNow = TimezoneConverter.VietnamNow;
            var issuedAt = new DateTimeOffset(vietnamNow, TimezoneConverter.VietnamOffset);
            var expiresAt = issuedAt.Add(_lifetime);

            var payload = new RefreshPayload
            {
                AccountId = accountId,
                IssuedAt = issuedAt,
                ExpiresAt = expiresAt
            };

            await _db.StringSetAsync(key, JsonSerializer.Serialize(payload), _lifetime);

            return new RefreshTokenIssueResult
            {
                Token = token,
                ExpiresAt = payload.ExpiresAt.DateTime
            };
        }

        public async Task<RefreshTokenValidationResult?> ValidateAsync(string refreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return null;
            }

            if (!TryDecode(refreshToken, out var accountId, out var tokenId))
            {
                return null;
            }

            var key = BuildKey(tokenId);
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return null;
            }

            RefreshPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<RefreshPayload>(value!);
            }
            catch
            {
                return null;
            }

            if (payload == null || payload.AccountId != accountId)
            {
                return null;
            }

            return new RefreshTokenValidationResult
            {
                AccountId = payload.AccountId,
                TokenId = tokenId,
                ExpiresAt = payload.ExpiresAt.DateTime
            };
        }

        public Task RevokeAsync(string refreshToken, CancellationToken ct = default)
        {
            if (!TryDecode(refreshToken, out _, out var tokenId))
            {
                return Task.CompletedTask;
            }

            return _db.KeyDeleteAsync(BuildKey(tokenId));
        }

        private static string BuildKey(Guid tokenId) => $"{KeyPrefix}:{tokenId:D}";

        private static string Encode(Guid accountId, Guid tokenId)
        {
            var raw = $"{accountId:D}:{tokenId:D}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        private static bool TryDecode(string token, out Guid accountId, out Guid tokenId)
        {
            accountId = Guid.Empty;
            tokenId = Guid.Empty;
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
                var parts = raw.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    return false;
                }

                if (Guid.TryParse(parts[0], out accountId) && Guid.TryParse(parts[1], out tokenId))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            accountId = Guid.Empty;
            tokenId = Guid.Empty;
            return false;
        }

        private class RefreshPayload
        {
            public Guid AccountId { get; set; }
            public DateTimeOffset IssuedAt { get; set; }
            public DateTimeOffset ExpiresAt { get; set; }
        }
    }
}

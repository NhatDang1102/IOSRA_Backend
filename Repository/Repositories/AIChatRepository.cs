using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Repository.DataModels;
using Repository.Interfaces;
using StackExchange.Redis;

namespace Repository.Repositories
{
    public class AIChatRepository : IAIChatRepository
    {
        private const string KeyPrefix = "ai_chat:history";
        private static readonly TimeSpan HistoryTtl = TimeSpan.FromDays(7);
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IDatabase _redis;

        public AIChatRepository(IConnectionMultiplexer redis)
        {
            _redis = redis.GetDatabase();
        }

        public async Task<IReadOnlyList<AiChatStoredMessage>> GetHistoryAsync(Guid accountId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var key = BuildKey(accountId);
            var values = await _redis.ListRangeAsync(key).ConfigureAwait(false);
            var result = new List<AiChatStoredMessage>(values.Length);

            foreach (var value in values)
            {
                if (value.IsNullOrEmpty)
                {
                    continue;
                }

                try
                {
                    var item = JsonSerializer.Deserialize<AiChatStoredMessage>(value!, SerializerOptions);
                    if (item != null)
                    {
                        result.Add(item);
                    }
                }
                catch
                {
                    // ignore malformed entries
                }
            }

            return result;
        }

        public async Task AppendAsync(Guid accountId, IReadOnlyList<AiChatStoredMessage> messages, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (messages == null || messages.Count == 0)
            {
                return;
            }

            var key = BuildKey(accountId);
            var payloads = messages
                .Select(m => (RedisValue)JsonSerializer.Serialize(m, SerializerOptions))
                .ToArray();

            if (payloads.Length == 0)
            {
                return;
            }

            await _redis.ListRightPushAsync(key, payloads).ConfigureAwait(false);
            await _redis.KeyExpireAsync(key, HistoryTtl).ConfigureAwait(false);
        }

        public async Task TrimAsync(Guid accountId, int maxMessages, CancellationToken ct = default)
        {
            if (maxMessages <= 0)
            {
                return;
            }

            ct.ThrowIfCancellationRequested();
            var key = BuildKey(accountId);
            var length = await _redis.ListLengthAsync(key).ConfigureAwait(false);
            if (length <= maxMessages)
            {
                return;
            }

            var startIndex = length - maxMessages;
            await _redis.ListTrimAsync(key, startIndex, -1).ConfigureAwait(false);
        }

        private static RedisKey BuildKey(Guid accountId) => $"{KeyPrefix}:{accountId:D}";
    }
}

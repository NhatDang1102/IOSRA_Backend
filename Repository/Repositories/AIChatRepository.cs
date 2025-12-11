using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.DataModels;
using Repository.DBContext;
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
        private readonly AppDbContext _context;

        public AIChatRepository(IConnectionMultiplexer redis, AppDbContext context)
        {
            _redis = redis.GetDatabase();
            _context = context;
        }

        public async Task<IReadOnlyList<string>> SearchContentAsync(IEnumerable<string> keywords, int limit = 5, CancellationToken ct = default)
        {
            var keywordList = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? new List<string>();
            if (keywordList.Count == 0)
            {
                return new List<string>();
            }

            // Ensure we search for distinct terms to avoid duplicate DB hits logic
            var distinctTerms = keywordList.Select(k => k.Trim()).Distinct().ToList();

            var results = new List<string>();

            // 1. Stories
            // Fetch a bit more to filter in memory safely
            var stories = await _context.stories
                .AsNoTracking()
                .Where(s => s.status == "published")
                .Select(s => new { s.title, s.desc, AuthorName = s.author.account.username })
                .OrderByDescending(s => s.title.Length) // heuristic: maybe irrelevant
                .Take(50) 
                .ToListAsync(ct);

            var matchingStories = stories
                .Where(s => distinctTerms.Any(term => 
                    (s.title != null && s.title.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (s.desc != null && s.desc.Contains(term, StringComparison.OrdinalIgnoreCase))))
                .Take(limit)
                .Select(s => $"[Story] Title: {s.title} | Author: {s.AuthorName} | Description: {s.desc}");

            results.AddRange(matchingStories);

            // 2. Chapters
            var chapters = await _context.chapter
                .AsNoTracking()
                .Where(c => c.status == "published")
                .Select(c => new { c.title, c.summary, StoryTitle = c.story.title, c.chapter_no })
                .OrderByDescending(c => c.chapter_no)
                .Take(100)
                .ToListAsync(ct);

            var matchingChapters = chapters
                .Where(c => distinctTerms.Any(term => 
                    (c.title != null && c.title.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                    (c.summary != null && c.summary.Contains(term, StringComparison.OrdinalIgnoreCase))))
                .Take(limit)
                .Select(c => $"[Chapter] Story: {c.StoryTitle} | Chapter {c.chapter_no}: {c.title} | Summary: {c.summary ?? "N/A"}");

            results.AddRange(matchingChapters);

            // 3. Authors
            // For authors, we can usually trust EF Like for simple containment
            // But let's stick to the pattern: fetch and filter for consistency if dataset is small.
            // Assuming author count isn't massive yet.
            var authors = await _context.authors
                .AsNoTracking()
                .Include(a => a.account)
                .Select(a => new { a.account.username, TotalStories = a.stories.Count(s => s.status == "published") })
                .Take(50)
                .ToListAsync(ct);

            var matchingAuthors = authors
                .Where(a => distinctTerms.Any(term => a.username != null && a.username.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Take(limit)
                .Select(a => $"[Author] Name: {a.username} | Published Stories: {a.TotalStories}");

            results.AddRange(matchingAuthors);

            return results.Take(limit * 2).ToList();
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

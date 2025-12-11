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
            var keywordList = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            if (keywordList == null || keywordList.Count == 0)
            {
                return new List<string>();
            }

            var results = new List<string>();

            // Helper to build OR predicate would be ideal, but for simplicity with EF Core 
            // and avoiding external lib dependencies, we fetch a bit more broadly then filter 
            // OR use multiple Contains/Like. 
            // Since EF Core 8 translates `OpenJson` or complex ORs well, but to be safe and simple:
            // We will loop but that's inefficient.
            // Better: Load potential candidates using the FIRST/most important keyword, then refine?
            // NO, user wants "All".
            
            // Let's use a simple approach: Get items matching ANY keyword.
            // Note: EF Core translation of `keywords.Any(k => Title.Contains(k))` is not always supported depending on provider version.
            // Client-side evaluation for search is bad for performance, but acceptable for MVP with low data volume.
            // For now, let's assume we iterate and take top results, or build a dynamic query if needed.
            // Given the constraints, let's fetch based on the raw list for now.
            
            // Actually, `keywords` is small (3-5 items).
            
            // 1. Stories
            var storyQuery = _context.stories.AsNoTracking().Where(s => s.status == "published");
            var storyMatches = await storyQuery
                .Select(s => new { s.title, s.desc, AuthorName = s.author.account.username })
                .ToListAsync(ct); // Pulling all published story metadata (usually acceptable for < 10k items)

            // In-memory filtering (to support complex "Any" logic robustly without dynamic LINQ)
            var matchingStories = storyMatches
                .Where(s => keywordList.Any(k => 
                    (s.title?.Contains(k, StringComparison.OrdinalIgnoreCase) == true) || 
                    (s.desc?.Contains(k, StringComparison.OrdinalIgnoreCase) == true)))
                .Take(limit)
                .Select(s => $"[Story] Title: {s.title} | Author: {s.AuthorName} | Description: {s.desc}");
            
            results.AddRange(matchingStories);

            // 2. Chapters (Only fetch fields needed)
            var chapterQuery = _context.chapter.AsNoTracking().Where(c => c.status == "published");
            // Optimization: If data is huge, this will be slow. But for "scanning all", full-text index is needed.
            // We'll proceed with in-memory for "Scanning" as requested, limiting fetch to fields.
            var chapterMatches = await chapterQuery
                .Select(c => new { c.title, c.summary, StoryTitle = c.story.title, c.chapter_no })
                .ToListAsync(ct);

            var matchingChapters = chapterMatches
                .Where(c => keywordList.Any(k => 
                    (c.title?.Contains(k, StringComparison.OrdinalIgnoreCase) == true) || 
                    (c.summary?.Contains(k, StringComparison.OrdinalIgnoreCase) == true)))
                .Take(limit)
                .Select(c => $"[Chapter] Story: {c.StoryTitle} | Chapter {c.chapter_no}: {c.title} | Summary: {c.summary ?? "N/A"}");

            results.AddRange(matchingChapters);

            // 3. Authors
            var authorMatches = await _context.authors.AsNoTracking()
                .Include(a => a.account)
                .Select(a => new { a.account.username, TotalStories = a.stories.Count(s => s.status == "published") })
                .ToListAsync(ct);

            var matchingAuthors = authorMatches
                .Where(a => keywordList.Any(k => a.username.Contains(k, StringComparison.OrdinalIgnoreCase)))
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

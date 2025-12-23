using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.DataModels;
using Repository.DBContext;
using Repository.Entities;
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
            // Làm sạch và lọc danh sách từ khóa
            var keywordList = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).ToList() ?? new List<string>();
            if (keywordList.Count == 0)
            {
                return new List<string>();
            }

            var distinctTerms = keywordList.Select(k => k.Trim()).Distinct().ToList();
            var results = new List<string>();

            // 1. Tìm kiếm Truyện (Stories)
            // Xây dựng query động để tìm trong TOÀN BỘ database thay vì giới hạn top 50
            IQueryable<Story> storyQuery = null;
            var baseStoryQuery = _context.stories.AsNoTracking().Where(s => s.status == "published");

            foreach (var term in distinctTerms)
            {
                // Tìm theo tiêu đề hoặc mô tả truyện
                var q = baseStoryQuery.Where(s => s.title.Contains(term) || s.desc.Contains(term));
                storyQuery = storyQuery == null ? q : storyQuery.Union(q);
            }

            if (storyQuery != null)
            {
                var stories = await storyQuery
                    .Select(s => new { s.title, s.desc, AuthorName = s.author.account.username })
                    .Take(limit)
                    .ToListAsync(ct);

                results.AddRange(stories.Select(s => $"[Story] Title: {s.title} | Author: {s.AuthorName} | Description: {s.desc}"));
            }

            // 2. Tìm kiếm Chương (Chapters)
            IQueryable<Chapter> chapterQuery = null;
            var baseChapterQuery = _context.chapter.AsNoTracking().Where(c => c.status == "published");

            foreach (var term in distinctTerms)
            {
                // Tìm theo tiêu đề hoặc tóm tắt chương
                var q = baseChapterQuery.Where(c => c.title.Contains(term) || c.summary.Contains(term));
                chapterQuery = chapterQuery == null ? q : chapterQuery.Union(q);
            }

            if (chapterQuery != null)
            {
                var chapters = await chapterQuery
                    .Select(c => new { c.title, c.summary, StoryTitle = c.story.title, c.chapter_no })
                    .OrderByDescending(c => c.chapter_no)
                    .Take(limit)
                    .ToListAsync(ct);

                results.AddRange(chapters.Select(c => $"[Chapter] Story: {c.StoryTitle} | Chapter {c.chapter_no}: {c.title} | Summary: {c.summary ?? "N/A"}"));
            }

            // 3. Tìm kiếm Tác giả (Authors)
            IQueryable<Author> authorQuery = null;
            var baseAuthorQuery = _context.authors.AsNoTracking().Include(a => a.account);

            foreach (var term in distinctTerms)
            {
                // Tìm theo tên tài khoản (username) của tác giả
                var q = baseAuthorQuery.Where(a => a.account.username.Contains(term));
                authorQuery = authorQuery == null ? q : authorQuery.Union(q);
            }

            if (authorQuery != null)
            {
                var authors = await authorQuery
                    .Select(a => new { a.account.username, TotalStories = a.stories.Count(s => s.status == "published") })
                    .Take(limit)
                    .ToListAsync(ct);

                results.AddRange(authors.Select(a => $"[Author] Name: {a.username} | Published Stories: {a.TotalStories}"));
            }

            // Trả về danh sách kết quả tổng hợp
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

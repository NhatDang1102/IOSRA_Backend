using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class StoryCatalogRepository : BaseRepository, IStoryCatalogRepository
    {
        private static readonly string[] PublicStoryStatuses = { "published", "completed" };

        public StoryCatalogRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<(List<story> Items, int Total)> SearchPublishedStoriesAsync(string? query, Guid? tagId, Guid? authorId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var storiesQuery = _db.stories
                .AsNoTracking()
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Where(s => PublicStoryStatuses.Contains(s.status));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim();
                var likePattern = BuildLikePattern(term);
                const string escapeChar = "\\";

                storiesQuery = storiesQuery.Where(s =>
                    EF.Functions.Like(s.title, likePattern, escapeChar) ||
                    (s.desc != null && EF.Functions.Like(s.desc, likePattern, escapeChar)));
            }

            if (tagId.HasValue && tagId.Value != Guid.Empty)
            {
                storiesQuery = storiesQuery.Where(s => s.story_tags.Any(st => st.tag_id == tagId.Value));
            }

            if (authorId.HasValue && authorId.Value != Guid.Empty)
            {
                storiesQuery = storiesQuery.Where(s => s.author_id == authorId.Value);
            }

            var total = await storiesQuery.CountAsync(ct);
            var skip = (page - 1) * pageSize;

            var items = await storiesQuery
                .OrderByDescending(s => s.published_at ?? s.updated_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<List<story>> GetLatestPublishedStoriesAsync(int limit, CancellationToken ct = default)
        {
            if (limit <= 0)
            {
                return new List<story>();
            }

            return await _db.stories
                .AsNoTracking()
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Where(s => PublicStoryStatuses.Contains(s.status))
                .OrderByDescending(s => s.published_at ?? s.updated_at)
                .Take(limit)
                .ToListAsync(ct);
        }

        public async Task<List<story>> GetStoriesByIdsAsync(IEnumerable<Guid> storyIds, CancellationToken ct = default)
        {
            var ids = storyIds?.Distinct().ToArray() ?? Array.Empty<Guid>();
            if (ids.Length == 0)
            {
                return new List<story>();
            }

            return await _db.stories
                .AsNoTracking()
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Where(s => ids.Contains(s.story_id) && PublicStoryStatuses.Contains(s.status))
                .ToListAsync(ct);
        }

        public Task<story?> GetPublishedStoryByIdAsync(Guid storyId, CancellationToken ct = default)
            => _db.stories
                  .AsNoTracking()
                  .Include(s => s.author).ThenInclude(a => a.account)
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(s => s.story_id == storyId && PublicStoryStatuses.Contains(s.status), ct);
        private static string BuildLikePattern(string term)
        {
            var escaped = term
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            return $"%{escaped}%";
        }
    }
}

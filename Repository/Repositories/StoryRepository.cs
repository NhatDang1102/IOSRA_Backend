using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Repository.Repositories
{
    public class StoryRepository : BaseRepository, IStoryRepository
    {
        private static readonly string[] PublicStoryStatuses = { "published", "completed" };

        public StoryRepository(AppDbContext db) : base(db)
        {
        }

        public Task<author?> GetAuthorAsync(Guid accountId, CancellationToken ct = default)
            => _db.authors
                  .Include(a => a.rank)
                  .Include(a => a.account)
                  .FirstOrDefaultAsync(a => a.account_id == accountId, ct);

        public Task<List<tag>> GetTagsByIdsAsync(IEnumerable<Guid> tagIds, CancellationToken ct = default)
        {
            var ids = tagIds.Distinct().ToArray();
            return _db.tags.Where(t => ids.Contains(t.tag_id)).ToListAsync(ct);
        }

        public async Task<story> AddStoryAsync(story entity, IEnumerable<Guid> tagIds, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(story.story_id));
            _db.stories.Add(entity);

            var tags = tagIds.Distinct().ToArray();
            if (tags.Length > 0)
            {
                foreach (var tagId in tags)
                {
                    _db.story_tags.Add(new story_tag
                    {
                        story_id = entity.story_id,
                        tag_id = tagId
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public Task<List<story>> GetStoriesByAuthorAsync(Guid authorId, IEnumerable<string>? statuses = null, CancellationToken ct = default)
        {
            var query = _db.stories
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Where(s => s.author_id == authorId);

            if (statuses is not null)
            {
                var statusList = statuses
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToArray();

                if (statusList.Length > 0)
                {
                    query = query.Where(s => statusList.Contains(s.status.ToLower()));
                }
                else
                {
                    return Task.FromResult(new List<story>());
                }
            }

            return query
                .OrderByDescending(s => s.updated_at)
                .ToListAsync(ct);
        }

        public Task<story?> GetStoryWithDetailsAsync(Guid storyId, CancellationToken ct = default)
            => _db.stories
                  .Include(s => s.author).ThenInclude(a => a.account)
                  .Include(s => s.author).ThenInclude(a => a.rank)
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(s => s.story_id == storyId, ct);

        public Task<story?> GetStoryForAuthorAsync(Guid storyId, Guid authorId, CancellationToken ct = default)
            => _db.stories
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(s => s.story_id == storyId && s.author_id == authorId, ct);

        public async Task UpdateStoryAsync(story entity, CancellationToken ct = default)
        {
            _db.stories.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task ReplaceStoryTagsAsync(Guid storyId, IEnumerable<Guid> tagIds, CancellationToken ct = default)
        {
            var tagSet = tagIds.Distinct().ToArray();
            var existing = await _db.story_tags.Where(st => st.story_id == storyId).ToListAsync(ct);

            if (existing.Count > 0)
            {
                _db.story_tags.RemoveRange(existing);
            }

            foreach (var tagId in tagSet)
            {
                _db.story_tags.Add(new story_tag
                {
                    story_id = storyId,
                    tag_id = tagId
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(content_approve.review_id));
            entity.created_at = entity.created_at == default ? DateTime.UtcNow : entity.created_at;
            _db.content_approves.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<content_approve?> GetContentApprovalForStoryAsync(Guid storyId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story")
                  .OrderByDescending(c => c.created_at)
                  .FirstOrDefaultAsync(ct);

        public Task<content_approve?> GetContentApprovalByIdAsync(Guid reviewId, CancellationToken ct = default)
            => _db.content_approves
                  .Include(c => c.story!)
                      .ThenInclude(s => s.author!)
                      .ThenInclude(a => a.account)
                  .Include(c => c.story!)
                      .ThenInclude(s => s.author!)
                      .ThenInclude(a => a.rank)
                  .Include(c => c.story!)
                      .ThenInclude(s => s.story_tags)
                      .ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(c => c.review_id == reviewId, ct);

        public Task<List<content_approve>> GetContentApprovalsForStoryAsync(Guid storyId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story")
                  .OrderByDescending(c => c.created_at)
                  .ToListAsync(ct);

        public Task<List<story>> GetStoriesForModerationAsync(IEnumerable<string> statuses, CancellationToken ct = default)
        {
            var statusList = statuses?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct()
                .ToArray() ?? Array.Empty<string>();

            if (statusList.Length == 0)
            {
                return Task.FromResult(new List<story>());
            }

            return _db.stories
                .Include(s => s.author).ThenInclude(a => a.account)
                .Include(s => s.author).ThenInclude(a => a.rank)
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Include(s => s.content_approves)
                .Where(s => statusList.Contains(s.status.ToLower()))
                .OrderByDescending(s => s.updated_at)
                .ToListAsync(ct);
        }

        public async Task<(List<story> Items, int Total)> SearchPublishedStoriesAsync(string? query, Guid? tagId, int page, int pageSize, CancellationToken ct = default)
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
                var likeTerm = $"%{term}%";
                storiesQuery = storiesQuery.Where(s =>
                    EF.Functions.Like(s.title, likeTerm) ||
                    (s.desc != null && EF.Functions.Like(s.desc, likeTerm)) ||
                    EF.Functions.Like(s.author.account.username, likeTerm));
            }

            if (tagId.HasValue)
            {
                storiesQuery = storiesQuery.Where(s => s.story_tags.Any(st => st.tag_id == tagId.Value));
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

        public Task<story?> GetPublishedStoryByIdAsync(Guid storyId, CancellationToken ct = default)
            => _db.stories
                  .AsNoTracking()
                  .Include(s => s.author).ThenInclude(a => a.account)
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(s => s.story_id == storyId && PublicStoryStatuses.Contains(s.status), ct);

        public Task<bool> AuthorHasPendingStoryAsync(Guid authorId, Guid? excludeStoryId = null, CancellationToken ct = default)
        {
            var query = _db.stories.Where(s => s.author_id == authorId && s.status == "pending");
            if (excludeStoryId.HasValue)
            {
                var excluded = excludeStoryId.Value;
                query = query.Where(s => s.story_id != excluded);
            }

            return query.AnyAsync(ct);
        }

        public Task<bool> AuthorHasUncompletedPublishedStoryAsync(Guid authorId, CancellationToken ct = default)
            => _db.stories.AnyAsync(s => s.author_id == authorId && s.status == "published", ct);

        public Task<DateTime?> GetLastStoryRejectedAtAsync(Guid storyId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story" && c.status == "rejected")
                  .OrderByDescending(c => c.created_at)
                  .Select(c => (DateTime?)c.created_at)
                  .FirstOrDefaultAsync(ct);

        public Task<DateTime?> GetLastAuthorStoryRejectedAtAsync(Guid authorId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.approve_type == "story" && c.status == "rejected" && c.story != null && c.story.author_id == authorId)
                  .OrderByDescending(c => c.created_at)
                  .Select(c => (DateTime?)c.created_at)
                  .FirstOrDefaultAsync(ct);

        public Task<int> GetChapterCountAsync(Guid storyId, CancellationToken ct = default)
            => _db.chapters.CountAsync(c => c.story_id == storyId, ct);

        public Task<DateTime?> GetStoryPublishedAtAsync(Guid storyId, CancellationToken ct = default)
            => _db.stories
                  .Where(s => s.story_id == storyId)
                  .Select(s => (DateTime?)s.published_at)
                  .FirstOrDefaultAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}



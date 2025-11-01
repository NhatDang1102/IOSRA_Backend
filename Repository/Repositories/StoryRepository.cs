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

        public Task<List<story>> GetStoriesByAuthorAsync(Guid authorId, CancellationToken ct = default)
            => _db.stories
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .Where(s => s.author_id == authorId)
                  .ToListAsync(ct);

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
            _db.content_approves.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<content_approve?> GetLatestContentApproveAsync(Guid storyId, string source, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story" && c.source == source)
                  .OrderByDescending(c => c.created_at)
                  .FirstOrDefaultAsync(ct);

        public Task<List<content_approve>> GetContentApprovalsForStoryAsync(Guid storyId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story")
                  .OrderByDescending(c => c.created_at)
                  .ToListAsync(ct);

        public Task<List<story>> GetStoriesPendingHumanReviewAsync(CancellationToken ct = default)
            => _db.stories
                  .Include(s => s.author).ThenInclude(a => a.account)
                  .Include(s => s.author).ThenInclude(a => a.rank)
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .Where(s => s.status == "pending")
                  .OrderBy(s => s.created_at)
                  .ToListAsync(ct);

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

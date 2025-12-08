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
using Repository.Utils;

namespace Repository.Repositories
{
    public class AuthorStoryRepository : BaseRepository, IAuthorStoryRepository
    {
        public AuthorStoryRepository(AppDbContext db) : base(db)
        {
        }


        //lấy profile của tác giả có kèm rank 
        public Task<author?> GetAuthorAsync(Guid accountId, CancellationToken ct = default)
            => _db.authors
                  .Include(a => a.rank)
                  .Include(a => a.account)
                  .FirstOrDefaultAsync(a => a.account_id == accountId, ct);


        //get list tag từ id (distinct để tách biệt trong list)
        public Task<List<tag>> GetTagsByIdsAsync(IEnumerable<Guid> tagIds, CancellationToken ct = default)
        {
            var ids = tagIds.Distinct().ToArray();
            return _db.tag.Where(t => ids.Contains(t.tag_id)).ToListAsync(ct);
        }

        public async Task<story> CreateAsync(story entity, IEnumerable<Guid> tagIds, CancellationToken ct = default)
        {

            //check trong entity đã đc gán story_id chưa trc khi đẩy vô db (tránh db tự tạo)
            EnsureId(entity, nameof(story.story_id));

            //đẩy entity vào context 
            _db.stories.Add(entity);

            //distinct để tách hết tag id nào bị trùng trong array
            var tags = tagIds.Distinct().ToArray();

            //gán story_tag
            if (tags.Length > 0)
            {
                foreach (var tagId in tags)
                {
                    _db.story_tag.Add(new story_tag
                    {
                        story_id = entity.story_id,
                        tag_id = tagId
                    });
                }
            }
            //chạy insert vô db 
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public Task<List<story>> GetAllByAuthorAsync(Guid authorId, IEnumerable<string>? statuses = null, CancellationToken ct = default)
        {
            //bóc ra líst stor của author kèm luôn tag và trạng thái kiểm duyệt từng story 
            var query = _db.stories
                .Include(s => s.story_tags).ThenInclude(st => st.tag)
                .Include(s => s.content_approves)
                .Where(s => s.author_id == authorId);

            //check request có kèm status (pending/published/draft....) để filter 
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

        public Task<story?> GetByIdForAuthorAsync(Guid storyId, Guid authorId, CancellationToken ct = default)
            => _db.stories
                  .Include(s => s.story_tags).ThenInclude(st => st.tag)
                  .FirstOrDefaultAsync(s => s.story_id == storyId && s.author_id == authorId, ct);

        public async Task UpdateAsync(story entity, CancellationToken ct = default)
        {
            _db.stories.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task ReplaceStoryTagsAsync(Guid storyId, IEnumerable<Guid> tagIds, CancellationToken ct = default)
        {
            var tagSet = tagIds.Distinct().ToArray();
            var existing = await _db.story_tag.Where(st => st.story_id == storyId).ToListAsync(ct);

            if (existing.Count > 0)
            {
                _db.story_tag.RemoveRange(existing);
            }

            foreach (var tagId in tagSet)
            {
                _db.story_tag.Add(new story_tag
                {
                    story_id = storyId,
                    tag_id = tagId
                });
            }

            await _db.SaveChangesAsync(ct);
        }
        //add vô bảng content_approve
        public async Task AddContentApproveAsync(content_approve entity, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(content_approve.review_id));
            entity.created_at = entity.created_at == default ? TimezoneConverter.VietnamNow : entity.created_at;
            _db.content_approves.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        //lấy bảng ghi kiểm duyệt mới nhất
        public Task<content_approve?> GetContentApprovalForStoryAsync(Guid storyId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story")
                  .OrderByDescending(c => c.created_at)
                  .FirstOrDefaultAsync(ct);


        //lấy hết (thật ra cũng ko xài vì override)
        public Task<List<content_approve>> GetContentApprovalsForStoryAsync(Guid storyId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story")
                  .OrderByDescending(c => c.created_at)
                  .ToListAsync(ct);


        //check coi author có story nào đang pending ko (để validate 1 author chỉ dc 1 truyện đang pending cùng lúc)
        public Task<bool> AuthorHasPendingStoryAsync(Guid authorId, Guid? excludeStoryId = null, CancellationToken ct = default)
        {
            var query = _db.stories.Where(s => s.author_id == authorId && s.status == "pending");
            if (excludeStoryId.HasValue)
            {
                query = query.Where(s => s.story_id != excludeStoryId.Value);
            }
            return query.AnyAsync(ct);
        }
        //giống cái ở trên nhưng cho published
        public Task<bool> AuthorHasUncompletedPublishedStoryAsync(Guid authorId, CancellationToken ct = default)
            => _db.stories.AnyAsync(s => s.author_id == authorId && s.status == "published", ct);

        //check cooldown bị rejected chống spam cho 1 story cụ thể (nhưng tạm thời đang cmt lại bên service) 
        public Task<DateTime?> GetLastStoryRejectedAtAsync(Guid storyId, CancellationToken ct = default)
            => _db.content_approves
                  .Where(c => c.story_id == storyId && c.approve_type == "story" && c.status == "rejected")
                  .OrderByDescending(c => c.created_at)
                  .Select(c => (DateTime?)c.created_at)
                  .FirstOrDefaultAsync(ct);

        //check bảng story (cái trên check content_approve) 
        public Task<DateTime?> GetLastAuthorStoryRejectedAtAsync(Guid authorId, CancellationToken ct = default)
            => _db.stories
                  .Where(s => s.author_id == authorId && s.status == "rejected")
                  .OrderByDescending(s => s.updated_at)
                  .Select(s => (DateTime?)s.updated_at)
                  .FirstOrDefaultAsync(ct);

        //đếm số chap 
        public Task<int> GetChapterCountAsync(Guid storyId, CancellationToken ct = default)
            => _db.chapter.CountAsync(c => c.story_id == storyId, ct);

        public Task<DateTime?> GetStoryPublishedAtAsync(Guid storyId, CancellationToken ct = default)
            => _db.stories
                  .Where(s => s.story_id == storyId)
                  .Select(s => s.published_at)
                  .FirstOrDefaultAsync(ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

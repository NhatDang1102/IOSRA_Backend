using Microsoft.EntityFrameworkCore;
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
    public class TagRepository : ITagRepository
    {
        private readonly AppDbContext _db;

        public TagRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<List<tag>> ListAsync(CancellationToken ct = default)
            => _db.tags
                  .OrderBy(t => t.tag_name)
                  .ToListAsync(ct);

        public Task<tag?> GetByIdAsync(Guid tagId, CancellationToken ct = default)
            => _db.tags.FirstOrDefaultAsync(t => t.tag_id == tagId, ct);

        public Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
        {
            var query = _db.tags.AsQueryable();
            if (excludeId.HasValue)
            {
                query = query.Where(t => t.tag_id != excludeId.Value);
            }
            return query.AnyAsync(t => t.tag_name == name, ct);
        }

        public async Task<tag> CreateAsync(string name, CancellationToken ct = default)
        {
            var entity = new tag
            {
                tag_id = Guid.NewGuid(),
                tag_name = name
            };
            _db.tags.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task UpdateAsync(tag entity, CancellationToken ct = default)
        {
            _db.tags.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<bool> HasStoriesAsync(Guid tagId, CancellationToken ct = default)
            => _db.story_tags.AnyAsync(st => st.tag_id == tagId, ct);

        public async Task DeleteAsync(tag entity, CancellationToken ct = default)
        {
            _db.tags.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<List<tag>> GetTopAsync(int limit, CancellationToken ct = default)
        {
            limit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 100);

            // Đếm số story published/completed đang gắn tag và sort theo usage desc, name asc
            var q =
                from t in _db.tags
                let usage =
                    (from st in _db.story_tags
                     join s in _db.stories on st.story_id equals s.story_id
                     where st.tag_id == t.tag_id
                           && (s.status == "published" || s.status == "completed")
                     select st).Count()
                orderby usage descending, t.tag_name ascending
                select t;

            return await q.Take(limit).ToListAsync(ct);
        }

        public async Task<List<tag>> ResolveAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var set = (ids ?? Array.Empty<Guid>()).ToHashSet();
            if (set.Count == 0) return new List<tag>();

            return await _db.tags
                .Where(t => set.Contains(t.tag_id))
                .OrderBy(t => t.tag_name)
                .ToListAsync(ct);
        }

        public async Task<List<tag>> SearchAsync(string term, int limit, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(term)) return new List<tag>();
            limit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);

            var q = term.Trim();

            // 1) Ưu tiên bắt đầu bằng q
            var starts = await _db.tags
                .Where(t => t.tag_name.StartsWith(q))     
                .OrderBy(t => t.tag_name)
                .Take(limit)
                .ToListAsync(ct);

            if (starts.Count >= limit) return starts;

            // 2) Bổ sung phần còn thiếu với Contains (loại trùng)
            var takenIds = starts.Select(t => t.tag_id).ToHashSet();
            var remain = limit - starts.Count;

            var contains = await _db.tags
                .Where(t => t.tag_name.Contains(q) && !takenIds.Contains(t.tag_id))
                .OrderBy(t => t.tag_name)
                .Take(remain)
                .ToListAsync(ct);

            starts.AddRange(contains);
            return starts;
        }

    }
}

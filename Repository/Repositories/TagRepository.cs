using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

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

        public Task<tag?> GetByIdAsync(uint tagId, CancellationToken ct = default)
            => _db.tags.FirstOrDefaultAsync(t => t.tag_id == tagId, ct);

        public Task<bool> ExistsByNameAsync(string name, uint? excludeId = null, CancellationToken ct = default)
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

        public Task<bool> HasStoriesAsync(uint tagId, CancellationToken ct = default)
            => _db.story_tags.AnyAsync(st => st.tag_id == tagId, ct);

        public async Task DeleteAsync(tag entity, CancellationToken ct = default)
        {
            _db.tags.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}

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
    public class FavoriteStoryRepository : BaseRepository, IFavoriteStoryRepository
    {
        public FavoriteStoryRepository(AppDbContext db) : base(db)
        {
        }

        public Task<favorite_story?> GetAsync(Guid readerId, Guid storyId, CancellationToken ct = default)
            => _db.favorite_stories
                .Include(f => f.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.account)
                .FirstOrDefaultAsync(f => f.reader_id == readerId && f.story_id == storyId, ct);

        public Task AddAsync(favorite_story entity, CancellationToken ct = default)
        {
            _db.favorite_stories.Add(entity);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(favorite_story entity, CancellationToken ct = default)
        {
            _db.favorite_stories.Remove(entity);
            return Task.CompletedTask;
        }

        public async Task<(IReadOnlyList<favorite_story> Items, int Total)> ListAsync(Guid readerId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.favorite_stories
                .AsNoTracking()
                .Include(f => f.story)
                    .ThenInclude(s => s.author)
                        .ThenInclude(a => a.account)
                .Where(f => f.reader_id == readerId);

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(f => f.created_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

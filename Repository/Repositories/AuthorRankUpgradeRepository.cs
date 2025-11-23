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
    public class AuthorRankUpgradeRepository : BaseRepository, IAuthorRankUpgradeRepository
    {
        public AuthorRankUpgradeRepository(AppDbContext db) : base(db)
        {
        }

        public Task<author?> GetAuthorAsync(Guid authorId, CancellationToken ct = default)
            => _db.authors
                  .Include(a => a.account)
                  .Include(a => a.rank)
                  .FirstOrDefaultAsync(a => a.account_id == authorId, ct);

        public Task<bool> HasPublishedStoryAsync(Guid authorId, CancellationToken ct = default)
            => _db.stories.AnyAsync(s => s.author_id == authorId && s.status == "published", ct);

        public Task<bool> HasPendingRequestAsync(Guid authorId, CancellationToken ct = default)
            => _db.author_rank_upgrade_requests.AnyAsync(r => r.author_id == authorId && r.status == "pending", ct);

        public async Task<author_rank_upgrade_request> CreateAsync(author_rank_upgrade_request entity, CancellationToken ct = default)
        {
            entity.request_id = NewId();
            entity.created_at = TimezoneConverter.VietnamNow;
            entity.status = "pending";

            _db.author_rank_upgrade_requests.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<IReadOnlyList<author_rank_upgrade_request>> GetRequestsByAuthorAsync(Guid authorId, CancellationToken ct = default)
        {
            return await _db.author_rank_upgrade_requests
                .Include(r => r.author).ThenInclude(a => a.account)
                .Include(r => r.author).ThenInclude(a => a.rank)
                .Include(r => r.target_rank)
                .Include(r => r.moderator)
                .Where(r => r.author_id == authorId)
                .OrderByDescending(r => r.created_at)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<author_rank_upgrade_request>> ListAsync(string? status, CancellationToken ct = default)
        {
            var query = _db.author_rank_upgrade_requests
                .Include(r => r.author).ThenInclude(a => a.account)
                .Include(r => r.author).ThenInclude(a => a.rank)
                .Include(r => r.target_rank)
                .Include(r => r.moderator)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToLowerInvariant();
                query = query.Where(r => r.status == normalized);
            }

            return await query
                .OrderBy(r => r.status)
                .ThenByDescending(r => r.created_at)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public Task<author_rank_upgrade_request?> GetByIdAsync(Guid requestId, CancellationToken ct = default)
            => _db.author_rank_upgrade_requests
                  .Include(r => r.author).ThenInclude(a => a.account)
                  .Include(r => r.author).ThenInclude(a => a.rank)
                  .Include(r => r.target_rank)
                  .Include(r => r.moderator)
                  .FirstOrDefaultAsync(r => r.request_id == requestId, ct);

        public async Task UpdateAsync(author_rank_upgrade_request entity, CancellationToken ct = default)
        {
            _db.author_rank_upgrade_requests.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<List<author_rank>> GetAllRanksAsync(CancellationToken ct = default)
            => _db.author_ranks
                  .OrderBy(r => r.min_followers)
                  .AsNoTracking()
                  .ToListAsync(ct);

        public async Task UpdateAuthorRankAsync(Guid authorId, Guid targetRankId, CancellationToken ct = default)
        {
            var author = await _db.authors.FirstOrDefaultAsync(a => a.account_id == authorId, ct)
                         ?? throw new InvalidOperationException("Author not found when updating rank.");

            author.rank_id = targetRankId;
            await _db.SaveChangesAsync(ct);
        }
    }
}

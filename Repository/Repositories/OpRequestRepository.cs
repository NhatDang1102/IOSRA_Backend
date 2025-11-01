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
    public class OpRequestRepository : BaseRepository, IOpRequestRepository
    {
        public OpRequestRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<op_request> CreateUpgradeRequestAsync(Guid accountId, string? content, CancellationToken ct = default)
        {
            var req = new op_request
            {
                request_id = NewId(),
                requester_id = accountId,
                request_type = "become_author",
                request_content = content,
                withdraw_amount = null,
                omod_id = null,
                status = "pending",
                withdraw_code = null,
                created_at = DateTime.UtcNow
            };

            _db.op_requests.Add(req);
            await _db.SaveChangesAsync(ct);
            return req;
        }

        public async Task<List<op_request>> ListRequestsAsync(string? status, CancellationToken ct = default)
        {
            var query = _db.op_requests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.status == status);
            }
            return await query.OrderByDescending(r => r.created_at).ToListAsync(ct);
        }

        public Task<List<op_request>> ListRequestsOfRequesterAsync(Guid accountId, CancellationToken ct = default)
            => _db.op_requests
                  .Where(r => r.requester_id == accountId)
                  .OrderByDescending(r => r.created_at)
                  .ToListAsync(ct);

        public Task<op_request?> GetRequestAsync(Guid requestId, CancellationToken ct = default)
            => _db.op_requests.FirstOrDefaultAsync(r => r.request_id == requestId, ct);

        public async Task SetRequestApprovedAsync(Guid requestId, Guid omodId, CancellationToken ct = default)
        {
            var req = await _db.op_requests.FirstOrDefaultAsync(r => r.request_id == requestId, ct);
            if (req is null) return;

            req.status = "approved";
            req.omod_id = omodId;
            await _db.SaveChangesAsync(ct);
        }

        public async Task SetRequestRejectedAsync(Guid requestId, Guid omodId, string? reason, CancellationToken ct = default)
        {
            var req = await _db.op_requests.FirstOrDefaultAsync(r => r.request_id == requestId, ct);
            if (req is null) return;

            req.status = "rejected";
            req.omod_id = omodId;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                req.request_content = (req.request_content ?? string.Empty) + $"\n\n[REJECT_REASON]: {reason}";
            }

            await _db.SaveChangesAsync(ct);
        }

        public Task<bool> AuthorIsUnrestrictedAsync(Guid accountId, CancellationToken ct = default)
            => _db.authors.AnyAsync(a => a.account_id == accountId && !a.restricted, ct);

        public async Task EnsureAuthorUpgradedAsync(Guid accountId, Guid rankId, CancellationToken ct = default)
        {
            var author = await _db.authors.FirstOrDefaultAsync(x => x.account_id == accountId, ct);
            if (author is null)
            {
                _db.authors.Add(new author
                {
                    account_id = accountId,
                    restricted = false,
                    verified_status = false,
                    rank_id = rankId,
                    total_story = 0,
                    total_follower = 0
                });
            }
            else
            {
                author.restricted = false;
                author.rank_id = rankId;
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<Guid?> GetRankIdByNameAsync(string rankName, CancellationToken ct = default)
        {
            return await _db.author_ranks
                              .Where(r => r.rank_name == rankName)
                              .Select(r => (Guid?)r.rank_id)
                              .FirstOrDefaultAsync(ct);
        }

        public async Task<Guid?> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
        {
            return await _db.roles
                              .Where(r => r.role_code == roleCode)
                              .Select(r => (Guid?)r.role_id)
                              .FirstOrDefaultAsync(ct);
        }

        public async Task AddAccountRoleIfNotExistsAsync(Guid accountId, Guid roleId, CancellationToken ct = default)
        {
            var exists = await _db.account_roles.AnyAsync(ar => ar.account_id == accountId && ar.role_id == roleId, ct);
            if (!exists)
            {
                _db.account_roles.Add(new account_role { account_id = accountId, role_id = roleId });
                await _db.SaveChangesAsync(ct);
            }
        }

        public Task<bool> HasPendingAsync(Guid accountId, CancellationToken ct = default)
            => _db.op_requests.AnyAsync(r => r.requester_id == accountId && r.status == "pending", ct);

        public Task<DateTime?> GetLastRejectedAtAsync(Guid accountId, CancellationToken ct = default)
            => _db.op_requests
                  .Where(r => r.requester_id == accountId && r.status == "rejected")
                  .OrderByDescending(r => r.created_at)
                  .Select(r => (DateTime?)r.created_at)
                  .FirstOrDefaultAsync(ct);
    }
}

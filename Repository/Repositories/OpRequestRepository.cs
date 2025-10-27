using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class OpRequestRepository : IOpRequestRepository
    {
        private readonly AppDbContext _db;
        public OpRequestRepository(AppDbContext db) => _db = db;

        // Tạo request upgrade
        public async Task<op_request> CreateUpgradeRequestAsync(ulong accountId, string? content, CancellationToken ct = default)
        {
            var req = new op_request
            {
                requester_id = accountId,   // dùng requester_id
                request_type = "other",
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

        // Lấy list request (toàn hệ thống, filter theo status)
        public async Task<List<op_request>> ListRequestsAsync(string? status, CancellationToken ct = default)
        {
            var q = _db.op_requests.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(r => r.status == status);
            return await q.OrderByDescending(r => r.created_at).ToListAsync(ct);
        }

        // Lấy list request của 1 requester
        public Task<List<op_request>> ListRequestsOfRequesterAsync(ulong accountId, CancellationToken ct = default)
            => _db.op_requests
                  .Where(r => r.requester_id == accountId)
                  .OrderByDescending(r => r.created_at)
                  .ToListAsync(ct);

        // Lấy chi tiết 1 request
        public Task<op_request?> GetRequestAsync(ulong requestId, CancellationToken ct = default)
            => _db.op_requests.FirstOrDefaultAsync(r => r.request_id == requestId, ct);

        // Set approved
        public async Task SetRequestApprovedAsync(ulong requestId, ulong omodId, CancellationToken ct = default)
        {
            var req = await _db.op_requests.FirstOrDefaultAsync(r => r.request_id == requestId, ct);
            if (req is null) return;
            req.status = "approved";
            req.omod_id = omodId;
            await _db.SaveChangesAsync(ct);
        }

        // Set rejected
        public async Task SetRequestRejectedAsync(ulong requestId, ulong omodId, string? reason, CancellationToken ct = default)
        {
            var req = await _db.op_requests.FirstOrDefaultAsync(r => r.request_id == requestId, ct);
            if (req is null) return;
            req.status = "rejected";
            req.omod_id = omodId;
            if (!string.IsNullOrWhiteSpace(reason))
                req.request_content = (req.request_content ?? string.Empty) + $"\n\n[REJECT_REASON]: {reason}";
            await _db.SaveChangesAsync(ct);
        }

        // Đã là author unrestricted?
        public Task<bool> AuthorIsUnrestrictedAsync(ulong accountId, CancellationToken ct = default)
            => _db.authors.AnyAsync(a => a.account_id == accountId && !a.restricted, ct);

        // Đảm bảo upgrade lên author
        public async Task EnsureAuthorUpgradedAsync(ulong accountId, ushort rankId, CancellationToken ct = default)
        {
            var a = await _db.authors.FirstOrDefaultAsync(x => x.account_id == accountId, ct);
            if (a is null)
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
                a.restricted = false;
                a.rank_id = rankId;
            }
            await _db.SaveChangesAsync(ct);
        }

        // Lấy rankId theo rank_name
        public async Task<ushort> GetRankIdByNameAsync(string rankName, CancellationToken ct = default)
        {
            var id = await _db.author_ranks
                              .Where(r => r.rank_name == rankName)
                              .Select(r => r.rank_id)
                              .FirstOrDefaultAsync(ct);
            return id; // 0 nếu chưa seed
        }

        // Lấy roleId theo role_code
        public async Task<ushort> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
        {
            var id = await _db.roles
                              .Where(r => r.role_code == roleCode)
                              .Select(r => r.role_id)
                              .FirstOrDefaultAsync(ct);
            return id; // 0 nếu chưa seed
        }

        // Thêm role nếu chưa có
        public async Task AddAccountRoleIfNotExistsAsync(ulong accountId, ushort roleId, CancellationToken ct = default)
        {
            var exists = await _db.account_roles.AnyAsync(ar => ar.account_id == accountId && ar.role_id == roleId, ct);
            if (!exists)
            {
                _db.account_roles.Add(new account_role { account_id = accountId, role_id = roleId });
                await _db.SaveChangesAsync(ct);
            }
        }

        // Đang có pending?
        public Task<bool> HasPendingAsync(ulong accountId, CancellationToken ct = default)
            => _db.op_requests.AnyAsync(r => r.requester_id == accountId && r.status == "pending", ct);

        // Thời điểm reject gần nhất
        public Task<DateTime?> GetLastRejectedAtAsync(ulong accountId, CancellationToken ct = default)
            => _db.op_requests
                  .Where(r => r.requester_id == accountId && r.status == "rejected")
                  .OrderByDescending(r => r.created_at)
                  .Select(r => (DateTime?)r.created_at)
                  .FirstOrDefaultAsync(ct);
    }
}

using Contract.DTOs.Request.Admin;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Repositories
{
    /// <summary>
    /// Repository dành riêng cho tác vụ quản trị. 
    /// Dùng AppDbContext trực tiếp để tránh sửa interface cũ.
    /// </summary>
    public sealed class AdminRepository : IAdminRepository
    {
        private readonly AppDbContext _db;

        public AdminRepository(AppDbContext db) => _db = db;

        public async Task<(IReadOnlyList<account> items, int total)> QueryAccountsAsync(AccountQuery q, CancellationToken ct)
        {
            IQueryable<account> query = _db.accounts.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim();
                query = query.Where(a => a.username.Contains(s) || a.email.Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(q.Status))
            {
                var st = q.Status.Trim();
                query = query.Where(a => a.status == st);
            }

            var total = await query.CountAsync(ct);

            var skip = (q.Page - 1) * q.PageSize;
            if (skip < 0) skip = 0;

            var items = await query
                .OrderByDescending(a => a.created_at)
                .Skip(skip).Take(q.PageSize)
                .AsNoTracking()
                .ToListAsync(ct);

            return (items, total);
        }

        public Task<account?> GetAccountAsync(ulong accountId, CancellationToken ct) =>
            _db.accounts.FirstOrDefaultAsync(a => a.account_id == accountId, ct)!;

        public async Task SetStatusAsync(ulong accountId, string status, CancellationToken ct)
        {
            var acc = await _db.accounts.FirstOrDefaultAsync(a => a.account_id == accountId, ct);
            if (acc is null) return;
            acc.status = status;
            acc.updated_at = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<List<string>> GetRoleCodesAsync(ulong accountId, CancellationToken ct)
        {
            return await _db.account_roles
                .Where(ar => ar.account_id == accountId)
                .Select(ar => ar.role.role_code)
                .ToListAsync(ct);
        }

        public async Task ReplaceRolesAsync(ulong accountId, IEnumerable<ushort> roleIds, CancellationToken ct)
        {
            // Xóa role cũ
            var olds = _db.account_roles.Where(r => r.account_id == accountId);
            _db.account_roles.RemoveRange(olds);

            // Thêm role mới
            var now = DateTime.UtcNow;
            foreach (var rid in roleIds.Distinct())
            {
                _db.account_roles.Add(new account_role
                {
                    account_id = accountId,
                    role_id = rid,
                    created_at = now,
                    updated_at = now
                });
            }
            await _db.SaveChangesAsync(ct);
        }
    }
}

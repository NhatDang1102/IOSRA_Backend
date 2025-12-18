using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DataModels;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;

namespace Repository.Repositories
{
    public class AdminRepository : BaseRepository, IAdminRepository
    {
        private static readonly string[] PrimaryRoleCodes = { "reader", "cmod", "omod", "admin" };

        public AdminRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<(IReadOnlyList<AdminAccountProjection> Items, int Total)> GetAccountsAsync(
            string? status,
            string? role,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default)
        {
            var normalizedStatus = status?.Trim().ToLowerInvariant();
            var normalizedRole = role?.Trim().ToLowerInvariant();

            var query = _db.accounts.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                query = query.Where(a => a.status == normalizedStatus);
            }

            if (!string.IsNullOrWhiteSpace(normalizedRole))
            {
                query = query.Where(a => a.account_roles.Any(ar => ar.role.role_code == normalizedRole));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a => a.username.Contains(search) || a.email.Contains(search));
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(a => a.created_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AdminAccountProjection
                {
                    AccountId = a.account_id,
                    Username = a.username,
                    Email = a.email,
                    Status = a.status,
                    Strike = a.strike,
                    StrikeStatus = a.strike_status ?? string.Empty,
                    StrikeRestrictedUntil = a.strike_restricted_until,
                    CreatedAt = a.created_at,
                    UpdatedAt = a.updated_at,
                    Roles = a.account_roles
                        .Select(ar => ar.role.role_code)
                        .ToList()
                })
                .ToListAsync(ct);

            return (items, total);
        }

        public Task<AdminAccountProjection?> GetAccountAsync(Guid accountId, CancellationToken ct = default)
            => _db.accounts
                .AsNoTracking()
                .Where(a => a.account_id == accountId)
                .Select(a => new AdminAccountProjection
                {
                    AccountId = a.account_id,
                    Username = a.username,
                    Email = a.email,
                    Status = a.status,
                    Strike = a.strike,
                    StrikeStatus = a.strike_status ?? string.Empty,
                    StrikeRestrictedUntil = a.strike_restricted_until,
                    CreatedAt = a.created_at,
                    UpdatedAt = a.updated_at,
                    Roles = a.account_roles
                        .Select(ar => ar.role.role_code)
                        .ToList()
                })
                .FirstOrDefaultAsync(ct);

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
            => _db.accounts.AnyAsync(a => a.email == email, ct);

        public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
            => _db.accounts.AnyAsync(a => a.username == username, ct);

        public Task AddAccountAsync(account entity, CancellationToken ct = default)
        {
            _db.accounts.Add(entity);
            return Task.CompletedTask;
        }

        public Task<bool> HasAuthorProfileAsync(Guid accountId, CancellationToken ct = default)
            => _db.authors.AnyAsync(a => a.account_id == accountId, ct);

        public async Task RemovePrimaryProfilesAsync(Guid accountId, CancellationToken ct = default)
        {
            var readerProfile = await _db.readers.FirstOrDefaultAsync(r => r.account_id == accountId, ct);
            if (readerProfile != null)
            {
                _db.readers.Remove(readerProfile);
            }

            var cmodProfile = await _db.ContentMods.FirstOrDefaultAsync(c => c.account_id == accountId, ct);
            if (cmodProfile != null)
            {
                _db.ContentMods.Remove(cmodProfile);
            }

            var omodProfile = await _db.OperationMods.FirstOrDefaultAsync(o => o.account_id == accountId, ct);
            if (omodProfile != null)
            {
                _db.OperationMods.Remove(omodProfile);
            }

            var adminProfile = await _db.admins.FirstOrDefaultAsync(a => a.account_id == accountId, ct);
            if (adminProfile != null)
            {
                _db.admins.Remove(adminProfile);
            }
        }

        public Task EnsureReaderProfileAsync(Guid accountId, CancellationToken ct = default)
        {
            var entity = new reader
            {
                account_id = accountId,
                gender = "unspecified",
                bio = null,
                birthdate = null
            };
            _db.readers.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddContentModProfileAsync(Guid accountId, string? phone, CancellationToken ct = default)
        {
            var entity = new ContentMod
            {
                account_id = accountId,
                assigned_date = TimezoneConverter.VietnamNow,
                phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                total_approved_chapters = 0,
                total_rejected_chapters = 0,
                total_approved_stories = 0,
                total_rejected_stories = 0,
                total_reported_handled = 0
            };
            _db.ContentMods.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddOperationModProfileAsync(Guid accountId, string? phone, CancellationToken ct = default)
        {
            var entity = new OperationMod
            {
                account_id = accountId,
                assigned_date = TimezoneConverter.VietnamNow,
                phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                reports_generated = 0
            };
            _db.OperationMods.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddAdminProfileAsync(Guid accountId, CancellationToken ct = default)
        {
            var entity = new admin
            {
                account_id = accountId,
                department = null,
                notes = null
            };
            _db.admins.Add(entity);
            return Task.CompletedTask;
        }

        public async Task RemovePrimaryRolesAsync(Guid accountId, CancellationToken ct = default)
        {
            var roleIds = await _db.role
                .Where(r => PrimaryRoleCodes.Contains(r.role_code))
                .Select(r => r.role_id)
                .ToListAsync(ct);

            if (roleIds.Count == 0)
            {
                return;
            }

            var toRemove = await _db.account_role
                .Where(ar => ar.account_id == accountId && roleIds.Contains(ar.role_id))
                .ToListAsync(ct);

            if (toRemove.Count > 0)
            {
                _db.account_role.RemoveRange(toRemove);
            }
        }

        public async Task AddRoleAsync(Guid accountId, string roleCode, CancellationToken ct = default)
        {
            var role = await _db.role.FirstOrDefaultAsync(r => r.role_code == roleCode, ct)
                       ?? throw new InvalidOperationException($"Role '{roleCode}' not found.");

            var entity = new account_role
            {
                account_id = accountId,
                role_id = role.role_id,
                created_at = TimezoneConverter.VietnamNow,
                updated_at = TimezoneConverter.VietnamNow
            };
            _db.account_role.Add(entity);
        }

        public async Task SetAccountStatusAsync(Guid accountId, string status, CancellationToken ct = default)
        {
            var account = await _db.accounts.FirstOrDefaultAsync(a => a.account_id == accountId, ct)
                          ?? throw new InvalidOperationException("Account not found.");

            account.status = status;
            account.updated_at = TimezoneConverter.VietnamNow;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

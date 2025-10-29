using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Yitter.IdGenerator;

namespace Repository.Repositories
{
    public class AuthRepository : IAuthRepository
    {
        private readonly AppDbContext _db;
        public AuthRepository(AppDbContext db) => _db = db;

        public Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default)
            => _db.accounts.AnyAsync(a => a.username == username || a.email == email, ct);

        public async Task<account> AddAccountAsync(account entity, CancellationToken ct = default)
        {
            // Gán Snowflake ID nếu chưa có
            if (entity.account_id == 0UL)
            {
                // YitIdHelper.NextId() -> long (signed); ép về ulong
                entity.account_id = unchecked((ulong)YitIdHelper.NextId());
            }

            _db.accounts.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public Task<account?> FindAccountByIdentifierAsync(string identifier, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.email == identifier || a.username == identifier, ct);

        public Task<account?> FindAccountByEmailAsync(string email, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.email == email, ct);

        public async Task UpdatePasswordHashAsync(ulong accountId, string newHash, CancellationToken ct = default)
        {
            var acc = await _db.accounts.FirstAsync(a => a.account_id == accountId, ct);
            acc.password_hash = newHash;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<reader> AddReaderAsync(reader entity, CancellationToken ct = default)
        {
            // reader.account_id là FK từ account, không phát ID ở đây
            _db.readers.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<ushort> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
        {
            // role_id SMALLINT UNSIGNED -> ushort
            var id = await _db.roles
                .Where(r => r.role_code == roleCode)
                .Select(r => r.role_id)
                .FirstOrDefaultAsync(ct);

            if (id == 0) throw new InvalidOperationException($"Role '{roleCode}' chưa được seed.");
            return id;
        }

        public Task<List<string>> GetRoleCodesOfAccountAsync(ulong accountId, CancellationToken ct = default)
            => _db.account_roles
                  .Where(ar => ar.account_id == accountId)
                  .Join(_db.roles, ar => ar.role_id, r => r.role_id, (ar, r) => r.role_code)
                  .ToListAsync(ct);

        public async Task AddAccountRoleAsync(ulong accountId, ushort roleId, CancellationToken ct = default)
        {
            _db.account_roles.Add(new account_role
            {
                account_id = accountId,
                role_id = roleId
            });
            await _db.SaveChangesAsync(ct);
        }
    }
}

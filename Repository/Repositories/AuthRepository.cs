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
    public class AuthRepository : IAuthRepository
    {
        private readonly AppDbContext _db;
        public AuthRepository(AppDbContext db) => _db = db;

        public Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default)
            => _db.accounts.AnyAsync(a => a.username == username || a.email == email, ct);

        public async Task<account> AddAccountAsync(account entity, CancellationToken ct = default)
        {
            if (entity.account_id == Guid.Empty)
            {
                entity.account_id = Guid.NewGuid();
            }

            _db.accounts.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public Task<account?> FindAccountByIdentifierAsync(string identifier, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.email == identifier || a.username == identifier, ct);

        public Task<account?> FindAccountByEmailAsync(string email, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.email == email, ct);

        public async Task UpdatePasswordHashAsync(Guid accountId, string newHash, CancellationToken ct = default)
        {
            var acc = await _db.accounts.FirstAsync(a => a.account_id == accountId, ct);
            acc.password_hash = newHash;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<reader> AddReaderAsync(reader entity, CancellationToken ct = default)
        {
            _db.readers.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<Guid> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
        {
            var role = await _db.roles
                .Where(r => r.role_code == roleCode)
                .FirstOrDefaultAsync(ct);

            if (role == null)
            {
                throw new InvalidOperationException($"Role '{roleCode}' has not been seeded.");
            }

            return role.role_id;
        }

        public Task<List<string>> GetRoleCodesOfAccountAsync(Guid accountId, CancellationToken ct = default)
            => _db.account_roles
                  .Where(ar => ar.account_id == accountId)
                  .Join(_db.roles, ar => ar.role_id, r => r.role_id, (ar, r) => r.role_code)
                  .ToListAsync(ct);

        public async Task AddAccountRoleAsync(Guid accountId, Guid roleId, CancellationToken ct = default)
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

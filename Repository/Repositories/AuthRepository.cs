using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

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
            _db.accounts.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public Task<account?> FindAccountByIdentifierAsync(string identifier, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.email == identifier || a.username == identifier, ct);

        public async Task<reader> AddReaderAsync(reader entity, CancellationToken ct = default)
        {
            _db.readers.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<ushort> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
        {
            var id = await _db.roles
                .Where(r => r.role_code == roleCode)
                .Select(r => r.role_id)
                .Cast<ushort>()
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

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
    // Repository xử lý các truy vấn database liên quan đến authentication
    public class AuthRepository : IAuthRepository
    {
        private readonly AppDbContext _db;
        public AuthRepository(AppDbContext db) => _db = db;

        // Kiểm tra xem username hoặc email đã tồn tại trong database chưa
        public Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default)
            => _db.accounts.AnyAsync(a => a.username == username || a.email == email, ct);

        // Thêm account mới vào database
        public async Task<account> AddAccountAsync(account entity, CancellationToken ct = default)
        {
            // Tạo GUID mới nếu chưa có
            if (entity.account_id == Guid.Empty)
            {
                entity.account_id = Guid.NewGuid();
            }

            _db.accounts.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        // Tìm account theo identifier (email hoặc username)
        public Task<account?> FindAccountByIdentifierAsync(string identifier, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.email == identifier || a.username == identifier, ct);

        // Tìm account theo email
        public Task<account?> FindAccountByEmailAsync(string email, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.email == email, ct);

        public Task<account?> FindAccountByIdAsync(Guid accountId, CancellationToken ct = default)
            => _db.accounts.FirstOrDefaultAsync(a => a.account_id == accountId, ct);

        // Cập nhật password hash cho account
        public async Task UpdatePasswordHashAsync(Guid accountId, string newHash, CancellationToken ct = default)
        {
            var acc = await _db.accounts.FirstAsync(a => a.account_id == accountId, ct);
            acc.password_hash = newHash;
            await _db.SaveChangesAsync(ct);
        }

        // Thêm bản ghi reader mới (bảng reader là role-specific table)
        public async Task<reader> AddReaderAsync(reader entity, CancellationToken ct = default)
        {
            _db.readers.Add(entity);
            await _db.SaveChangesAsync(ct);
            return entity;
        }

        // Lấy role ID từ role code - throw exception nếu role chưa được seed
        public async Task<Guid> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default)
        {
            var role = await _db.role
                .Where(r => r.role_code == roleCode)
                .FirstOrDefaultAsync(ct);

            if (role == null)
            {
                throw new InvalidOperationException($"Role '{roleCode}' has not been seeded.");
            }

            return role.role_id;
        }

        // Lấy danh sách role codes của một account (join account_roles với roles)
        public Task<List<string>> GetRoleCodesOfAccountAsync(Guid accountId, CancellationToken ct = default)
            => _db.account_role
                  .Where(ar => ar.account_id == accountId)
                  .Join(_db.role, ar => ar.role_id, r => r.role_id, (ar, r) => r.role_code)
                  .ToListAsync(ct);

        // Gán role cho account (thêm vào bảng junction account_roles)
        public async Task AddAccountRoleAsync(Guid accountId, Guid roleId, CancellationToken ct = default)
        {
            _db.account_role.Add(new account_role
            {
                account_id = accountId,
                role_id = roleId
            });
            await _db.SaveChangesAsync(ct);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Interfaces;

namespace Repository.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _db;
    public RoleRepository(AppDbContext db) => _db = db;

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
}


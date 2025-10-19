using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories;

public class AccountRoleRepository : IAccountRoleRepository
{
    private readonly AppDbContext _db;
    public AccountRoleRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ulong accountId, ushort roleId, CancellationToken ct = default)
    {
        _db.account_roles.Add(new account_role
        {
            account_id = accountId,
            role_id = roleId
        });
        await _db.SaveChangesAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _db;
    public AccountRepository(AppDbContext db) => _db = db;

    public Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default)
        => _db.accounts.AnyAsync(a => a.username == username || a.email == email, ct);

    public async Task<account> AddAsync(account entity, CancellationToken ct = default)
    {
        _db.accounts.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public Task<account?> FindByIdentifierAsync(string identifier, CancellationToken ct = default)
        => _db.accounts
              .FirstOrDefaultAsync(a => a.email == identifier || a.username == identifier, ct);
}

using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories;

public class ReaderRepository : IReaderRepository
{
    private readonly AppDbContext _db;
    public ReaderRepository(AppDbContext db) => _db = db;

    public async Task<reader> AddAsync(reader entity, CancellationToken ct = default)
    {
        _db.readers.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }
}

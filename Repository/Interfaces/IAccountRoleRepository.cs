namespace Repository.Interfaces;

public interface IAccountRoleRepository
{
    Task AddAsync(ulong accountId, ushort roleId, CancellationToken ct = default);
}
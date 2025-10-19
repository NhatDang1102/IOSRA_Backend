namespace Repository.Interfaces;

public interface IRoleRepository
{
    Task<ushort> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
}
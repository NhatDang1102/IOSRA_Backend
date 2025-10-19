namespace Repository.Interfaces;

public interface IRoleRepository
{
    Task<ushort> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
    Task<List<string>> GetRoleCodesOfAccountAsync(ulong accountId, CancellationToken ct = default);

}
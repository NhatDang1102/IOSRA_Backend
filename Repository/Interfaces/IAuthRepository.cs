using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAuthRepository
    {
        Task<bool> ExistsByUsernameOrEmailAsync(string username, string email, CancellationToken ct = default);
        Task<account> AddAccountAsync(account entity, CancellationToken ct = default);
        Task<account?> FindAccountByIdentifierAsync(string identifier, CancellationToken ct = default);

        Task<reader> AddReaderAsync(reader entity, CancellationToken ct = default);

        Task<ushort> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
        Task<List<string>> GetRoleCodesOfAccountAsync(ulong accountId, CancellationToken ct = default);

        Task AddAccountRoleAsync(ulong accountId, ushort roleId, CancellationToken ct = default);
        Task<account?> FindAccountByEmailAsync(string email, CancellationToken ct = default);
        Task UpdatePasswordHashAsync(ulong accountId, string newHash, CancellationToken ct = default);
    }

}

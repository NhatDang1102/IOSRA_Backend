using System;
using System.Collections.Generic;
using System.Threading;
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

        Task<Guid> GetRoleIdByCodeAsync(string roleCode, CancellationToken ct = default);
        Task<List<string>> GetRoleCodesOfAccountAsync(Guid accountId, CancellationToken ct = default);

        Task AddAccountRoleAsync(Guid accountId, Guid roleId, CancellationToken ct = default);
        Task<account?> FindAccountByEmailAsync(string email, CancellationToken ct = default);
        Task UpdatePasswordHashAsync(Guid accountId, string newHash, CancellationToken ct = default);
    }
}

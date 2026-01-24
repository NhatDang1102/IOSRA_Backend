using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.DataModels;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAdminRepository
    {
        Task<(IReadOnlyList<AdminAccountProjection> Items, int Total)> GetAccountsAsync(
            string? status,
            string? role,
            string? search,
            int page,
            int pageSize,
            CancellationToken ct = default);

        Task<AdminAccountProjection?> GetAccountAsync(Guid accountId, CancellationToken ct = default);
        Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
        Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
        Task AddAccountAsync(account entity, CancellationToken ct = default);
        Task<bool> HasAuthorProfileAsync(Guid accountId, CancellationToken ct = default);
        Task RemovePrimaryProfilesAsync(Guid accountId, CancellationToken ct = default);
        Task EnsureReaderProfileAsync(Guid accountId, CancellationToken ct = default);
        Task AddContentModProfileAsync(Guid accountId, string? phone, CancellationToken ct = default);
        Task AddOperationModProfileAsync(Guid accountId, string? phone, CancellationToken ct = default);
        Task AddAdminProfileAsync(Guid accountId, CancellationToken ct = default);
        Task RemovePrimaryRolesAsync(Guid accountId, CancellationToken ct = default);
        Task AddRoleAsync(Guid accountId, string roleCode, CancellationToken ct = default);
        Task SetAccountStatusAsync(Guid accountId, string status, CancellationToken ct = default);
        Task<(bool IsAuthor, long Balance, long Pending)> GetAuthorRevenueInfoAsync(Guid accountId, CancellationToken ct = default);
        Task HideAuthorContentAsync(Guid accountId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

using Contract.DTOs.Request.Admin;
using Repository.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository.Interfaces
{
    public interface IAdminRepository
    {
        Task<(IReadOnlyList<account> items, int total)> QueryAccountsAsync(AccountQuery q, CancellationToken ct);
        Task<account?> GetAccountAsync(Guid accountId, CancellationToken ct);
        Task SetStatusAsync(Guid accountId, string status, CancellationToken ct);
        Task<List<string>> GetRoleCodesAsync(Guid accountId, CancellationToken ct);
        Task ReplaceRolesAsync(Guid accountId, IEnumerable<Guid> roleIds, CancellationToken ct);
    }
}

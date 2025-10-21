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
        Task<account?> GetAccountAsync(ulong accountId, CancellationToken ct);
        Task SetStatusAsync(ulong accountId, string status, CancellationToken ct);
        Task<List<string>> GetRoleCodesAsync(ulong accountId, CancellationToken ct);
        Task ReplaceRolesAsync(ulong accountId, IEnumerable<ushort> roleIds, CancellationToken ct);
    }
}

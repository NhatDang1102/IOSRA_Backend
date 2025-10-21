using Contract.DTOs.Request.Admin;
using Contract.DTOs.Respond.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IAdminService
    {
        Task<PagedResult<AccountAdminResponse>> QueryAccountsAsync(AccountQuery q, CancellationToken ct);
        Task SetRolesAsync(ulong accountId, List<string> roleCodes, CancellationToken ct);
        Task BanAsync(ulong accountId, string? reason, CancellationToken ct);
        Task UnbanAsync(ulong accountId, string? reason, CancellationToken ct);
        Task<AccountAdminResponse> GetAccountByIdentifierAsync(string identifier, CancellationToken ct = default);
    }
}

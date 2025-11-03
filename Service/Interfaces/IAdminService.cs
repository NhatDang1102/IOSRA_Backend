using Contract.DTOs.Request.Admin;
using Contract.DTOs.Respond.Admin;
using Contract.DTOs.Respond.Common;
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
        Task SetRolesAsync(Guid accountId, List<string> roleCodes, CancellationToken ct);
        Task BanAsync(Guid accountId, string? reason, CancellationToken ct);
        Task UnbanAsync(Guid accountId, string? reason, CancellationToken ct);
        Task<AccountAdminResponse> GetAccountByIdentifierAsync(string identifier, CancellationToken ct = default);
    }
}

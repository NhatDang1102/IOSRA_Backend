using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Admin;
using Contract.DTOs.Response.Common;

namespace Service.Interfaces
{
    public interface IAdminService
    {
        Task<PagedResult<AdminAccountResponse>> GetAccountsAsync(string? status, string? role, string? search, int page, int pageSize, CancellationToken ct = default);
        Task<AdminAccountResponse> CreateContentModAsync(CreateModeratorRequest request, CancellationToken ct = default);
        Task<AdminAccountResponse> CreateOperationModAsync(CreateModeratorRequest request, CancellationToken ct = default);
        Task<AdminAccountResponse> UpdateStatusAsync(Guid accountId, UpdateAccountStatusRequest request, CancellationToken ct = default);
    }
}

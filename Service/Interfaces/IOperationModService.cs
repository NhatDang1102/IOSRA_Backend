using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.OperationMod;

namespace Service.Interfaces
{
    public interface IOperationModService
    {
        Task<List<OpRequestItemResponse>> ListAsync(string? status, CancellationToken ct = default);
        Task ApproveAsync(Guid requestId, Guid omodAccountId, CancellationToken ct = default);
        Task RejectAsync(Guid requestId, Guid omodAccountId, RejectAuthorUpgradeRequest req, CancellationToken ct = default);
        Task<IReadOnlyList<AuthorWithdrawRequestResponse>> ListWithdrawRequestsAsync(string? status, CancellationToken ct = default);
        Task ApproveWithdrawAsync(Guid requestId, Guid omodAccountId, ApproveWithdrawRequest request, CancellationToken ct = default);
        Task RejectWithdrawAsync(Guid requestId, Guid omodAccountId, RejectWithdrawRequest request, CancellationToken ct = default);
    }
}

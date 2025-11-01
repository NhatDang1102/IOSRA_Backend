using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Respond.OperationMod;

namespace Service.Interfaces
{
    public interface IOperationModService
    {
        Task<List<OpRequestItemResponse>> ListAsync(string? status, CancellationToken ct = default);
        Task ApproveAsync(Guid requestId, Guid omodAccountId, CancellationToken ct = default);
        Task RejectAsync(Guid requestId, Guid omodAccountId, RejectAuthorUpgradeRequest req, CancellationToken ct = default);
    }
}

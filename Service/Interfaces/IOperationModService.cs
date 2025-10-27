using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Respond.OperationMod;

namespace Service.Interfaces
{
    public interface IOperationModService
    {
        Task<List<OpRequestItemResponse>> ListAsync(string? status, CancellationToken ct = default);
        Task ApproveAsync(ulong requestId, ulong omodAccountId, CancellationToken ct = default);
        Task RejectAsync(ulong requestId, ulong omodAccountId, RejectAuthorUpgradeRequest req, CancellationToken ct = default);
    }
}

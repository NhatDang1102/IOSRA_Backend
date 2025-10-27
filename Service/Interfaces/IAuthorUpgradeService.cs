using Contract.DTOs.Request.Author;
using Contract.DTOs.Respond.Author;
using Contract.DTOs.Respond.OperationMod;

namespace Service.Interfaces
{
    public interface IAuthorUpgradeService
    {
        Task<AuthorUpgradeResponse> SubmitAsync(ulong accountId, SubmitAuthorUpgradeRequest req, CancellationToken ct = default);
        Task<List<OpRequestItemResponse>> ListMyRequestsAsync(ulong accountId, CancellationToken ct = default);
    }
}

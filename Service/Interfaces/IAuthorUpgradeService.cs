using Contract.DTOs.Request.Author;
using System;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.OperationMod;

namespace Service.Interfaces
{
    public interface IAuthorUpgradeService
    {
        Task<AuthorUpgradeResponse> SubmitAsync(Guid accountId, SubmitAuthorUpgradeRequest req, CancellationToken ct = default);
        Task<List<OpRequestItemResponse>> ListMyRequestsAsync(Guid accountId, CancellationToken ct = default);
        Task<AuthorRankStatusResponse> GetRankStatusAsync(Guid accountId, CancellationToken ct = default);
    }
}

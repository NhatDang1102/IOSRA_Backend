using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Respond.Author;

namespace Service.Interfaces
{
    public interface IAuthorRankPromotionService
    {
        Task<RankPromotionRequestResponse> SubmitAsync(Guid authorAccountId, RankPromotionSubmitRequest request, CancellationToken ct = default);
        Task<IReadOnlyList<RankPromotionRequestResponse>> ListMineAsync(Guid authorAccountId, CancellationToken ct = default);
        Task<IReadOnlyList<RankPromotionRequestResponse>> ListForModerationAsync(string? status, CancellationToken ct = default);
        Task ApproveAsync(Guid requestId, Guid omodAccountId, string? note, CancellationToken ct = default);
        Task RejectAsync(Guid requestId, Guid omodAccountId, RankPromotionRejectRequest request, CancellationToken ct = default);
    }
}

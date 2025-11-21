using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Subscription;

namespace Service.Interfaces
{
    public interface ISubscriptionService
    {
        Task<IReadOnlyList<SubscriptionPlanResponse>> GetPlansAsync(CancellationToken ct = default);
        Task<SubscriptionStatusResponse> GetStatusAsync(Guid accountId, CancellationToken ct = default);
        Task<SubscriptionClaimResponse> ClaimDailyAsync(Guid accountId, CancellationToken ct = default);
        Task ActivateSubscriptionAsync(Guid accountId, string planCode, CancellationToken ct = default);
    }
}

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Repository.Entities;

namespace Service.Interfaces
{
    public interface IAdminPricingService
    {
        // Topup
        Task<List<topup_pricing>> GetAllTopupPricingsAsync(CancellationToken ct = default);
        Task UpdateTopupPricingAsync(UpdateTopupPricingRequest request, CancellationToken ct = default);

        // Subscription
        Task<List<subscription_plan>> GetAllSubscriptionPlansAsync(CancellationToken ct = default);
        Task UpdateSubscriptionPlanAsync(UpdateSubscriptionPlanRequest request, CancellationToken ct = default);
    }
}

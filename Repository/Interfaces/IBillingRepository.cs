using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IBillingRepository
    {
        Task<topup_pricing?> GetDiaTopupPricingAsync(ulong amount, CancellationToken ct = default);
        Task<List<topup_pricing>> GetDiaTopupPricingsAsync(CancellationToken ct = default);
        Task<dia_wallet> GetOrCreateDiaWalletAsync(Guid accountId, CancellationToken ct = default);
        Task AddDiaPaymentAsync(dia_payment payment, CancellationToken ct = default);
        Task<dia_payment?> GetDiaPaymentByOrderCodeAsync(string orderCode, CancellationToken ct = default);
        Task AddWalletPaymentAsync(wallet_payment payment, CancellationToken ct = default);
        Task AddPaymentReceiptAsync(payment_receipt receipt, CancellationToken ct = default);

        Task<subscription_plan?> GetSubscriptionPlanAsync(string planCode, CancellationToken ct = default);
        Task AddSubscriptionPaymentAsync(subscription_payment payment, CancellationToken ct = default);
        Task<subscription_payment?> GetSubscriptionPaymentByOrderCodeAsync(string orderCode, CancellationToken ct = default);

        // Admin Settings
        Task<topup_pricing?> GetTopupPricingByIdAsync(Guid pricingId, CancellationToken ct = default);
        Task UpdateTopupPricingAsync(topup_pricing pricing, CancellationToken ct = default);
        Task<List<topup_pricing>> GetAllTopupPricingsAsync(CancellationToken ct = default); // Including inactive

        Task<List<subscription_plan>> GetAllSubscriptionPlansAsync(CancellationToken ct = default);
        Task UpdateSubscriptionPlanAsync(subscription_plan plan, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

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

        Task<voice_topup_pricing?> GetVoiceTopupPricingAsync(ulong amount, CancellationToken ct = default);
        Task<List<voice_topup_pricing>> GetVoiceTopupPricingsAsync(CancellationToken ct = default);
        Task<voice_wallet> GetOrCreateVoiceWalletAsync(Guid accountId, CancellationToken ct = default);
        Task AddVoicePaymentAsync(voice_payment payment, CancellationToken ct = default);
        Task<voice_payment?> GetVoicePaymentByOrderCodeAsync(string orderCode, CancellationToken ct = default);
        Task AddVoiceWalletPaymentAsync(voice_wallet_payment payment, CancellationToken ct = default);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface ISubscriptionRepository
    {
        Task<List<subscription_plan>> GetPlansAsync(CancellationToken ct = default);
        Task<subcription?> GetLatestActiveAsync(Guid accountId, DateTime now, CancellationToken ct = default);
        Task<subcription?> GetByPlanAsync(Guid accountId, string planCode, CancellationToken ct = default);
        Task<subscription_plan?> GetPlanAsync(string planCode, CancellationToken ct = default);
        Task<dia_wallet?> GetWalletAsync(Guid accountId, CancellationToken ct = default);
        Task AddWalletAsync(dia_wallet wallet, CancellationToken ct = default);
        Task AddSubscriptionAsync(subcription subscription, CancellationToken ct = default);
        void AddWalletPayment(wallet_payment payment);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

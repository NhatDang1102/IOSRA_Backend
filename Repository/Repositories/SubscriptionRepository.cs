using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class SubscriptionRepository : ISubscriptionRepository
    {
        private readonly AppDbContext _db;

        public SubscriptionRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<List<subscription_plan>> GetPlansAsync(CancellationToken ct = default)
            => _db.subscription_plans
                .AsNoTracking()
                .OrderBy(p => p.price_vnd)
                .ToListAsync(ct);

        public Task<subscription?> GetLatestActiveAsync(Guid accountId, DateTime now, CancellationToken ct = default)
            => _db.subscription
                .Include(s => s.plan_codeNavigation)
                .Where(s => s.user_id == accountId && s.end_at >= now)
                .OrderByDescending(s => s.end_at)
                .FirstOrDefaultAsync(ct);

        public Task<subscription?> GetByPlanAsync(Guid accountId, string planCode, CancellationToken ct = default)
            => _db.subscription.FirstOrDefaultAsync(s => s.user_id == accountId && s.plan_code == planCode, ct);

        public Task<subscription_plan?> GetPlanAsync(string planCode, CancellationToken ct = default)
            => _db.subscription_plans.FirstOrDefaultAsync(p => p.plan_code == planCode, ct);

        public Task<dia_wallet?> GetWalletAsync(Guid accountId, CancellationToken ct = default)
            => _db.dia_wallets.FirstOrDefaultAsync(w => w.account_id == accountId, ct);

        public Task AddWalletAsync(dia_wallet wallet, CancellationToken ct = default)
            => _db.dia_wallets.AddAsync(wallet, ct).AsTask();

        public Task AddSubscriptionAsync(subscription subscription, CancellationToken ct = default)
            => _db.subscription.AddAsync(subscription, ct).AsTask();

        public void AddWalletPayment(wallet_payment payment)
            => _db.wallet_payments.Add(payment);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

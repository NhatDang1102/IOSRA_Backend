using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class BillingRepository : BaseRepository, IBillingRepository
    {
        public BillingRepository(AppDbContext db) : base(db)
        {
        }

        //lấy giá nạp dias tương ứng dầu tiên 
        public Task<topup_pricing?> GetDiaTopupPricingAsync(ulong amount, CancellationToken ct = default)
            => _db.topup_pricings.AsNoTracking().FirstOrDefaultAsync(p => p.amount_vnd == amount && p.is_active, ct);

        //giống trên nhưng lấy cả list 
        public Task<List<topup_pricing>> GetDiaTopupPricingsAsync(CancellationToken ct = default)
            => _db.topup_pricings
                  .AsNoTracking()
                  .Where(p => p.is_active)
                  .OrderBy(p => p.amount_vnd)
                  .ToListAsync(ct);
        //tạo mới/lấy info wallet của user
        public async Task<dia_wallet> GetOrCreateDiaWalletAsync(Guid accountId, CancellationToken ct = default)
        {
            var wallet = await _db.dia_wallets.FirstOrDefaultAsync(w => w.account_id == accountId, ct);
            if (wallet == null)
            {
                wallet = new dia_wallet
                {
                    wallet_id = Guid.NewGuid(),
                    account_id = accountId,
                    balance_dias = 0,
                    locked_dias = 0,
                    updated_at = Repository.Utils.TimezoneConverter.VietnamNow
                };
                _db.dia_wallets.Add(wallet);
            }
            return wallet;
        }
        //thêm vô payment mới status pending 
        public Task AddDiaPaymentAsync(dia_payment payment, CancellationToken ct = default)
        {
            _db.dia_payments.Add(payment);
            return Task.CompletedTask;
        }
        //truy vấn payment theo order code và include cả wallet để update số dư sau webhook
        public Task<dia_payment?> GetDiaPaymentByOrderCodeAsync(string orderCode, CancellationToken ct = default)
            => _db.dia_payments
                .Include(p => p.wallet)
                .FirstOrDefaultAsync(p => p.order_code == orderCode, ct);
        //ghi lại ls gd ví (ví dụ nạp thêm thì topup và delta là dương)
        public Task AddWalletPaymentAsync(wallet_payment payment, CancellationToken ct = default)
        {
            _db.wallet_payments.Add(payment);
            return Task.CompletedTask;
        }
        //thêm vô receipt sau khi gd thành côn
        public Task AddPaymentReceiptAsync(payment_receipt receipt, CancellationToken ct = default)
        {
            _db.payment_receipts.Add(receipt);
            return Task.CompletedTask;
        }

        public Task<subscription_plan?> GetSubscriptionPlanAsync(string planCode, CancellationToken ct = default)
            => _db.subscription_plans.AsNoTracking().FirstOrDefaultAsync(p => p.plan_code == planCode, ct);

        public Task AddSubscriptionPaymentAsync(subscription_payment payment, CancellationToken ct = default)
        {
            _db.subscription_payments.Add(payment);
            return Task.CompletedTask;
        }

        public Task<subscription_payment?> GetSubscriptionPaymentByOrderCodeAsync(string orderCode, CancellationToken ct = default)
            => _db.subscription_payments.FirstOrDefaultAsync(p => p.order_code == orderCode, ct);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}

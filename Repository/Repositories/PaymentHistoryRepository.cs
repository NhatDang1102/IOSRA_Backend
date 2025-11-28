using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.DataModels;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class PaymentHistoryRepository : BaseRepository, IPaymentHistoryRepository
    {
        private const string TypeDia = "dia_topup";
        private const string TypeVoice = "voice_topup";
        private const string TypeSubscription = "subscription";

        public PaymentHistoryRepository(AppDbContext db) : base(db)
        {
        }

        public async Task<(List<PaymentHistoryRecord> Items, int Total)> GetHistoryAsync(
            Guid accountId,
            int page,
            int pageSize,
            string? type,
            string? status,
            DateTime? from,
            DateTime? to,
            CancellationToken ct = default)
        {
            var diaQuery = _db.dia_payments
                .AsNoTracking()
                .Where(p => p.wallet.account_id == accountId)
                .Select(p => new PaymentHistoryRecord
                {
                    PaymentId = p.topup_id,
                    Type = TypeDia,
                    Provider = p.provider,
                    OrderCode = p.order_code,
                    AmountVnd = p.amount_vnd,
                    GrantedValue = p.diamond_granted,
                    GrantedUnit = "dias",
                    Status = p.status,
                    CreatedAt = p.created_at
                });

            var voiceQuery = _db.voice_payments
                .AsNoTracking()
                .Where(p => p.voice_wallet.account_id == accountId)
                .Select(p => new PaymentHistoryRecord
                {
                    PaymentId = p.topup_id,
                    Type = TypeVoice,
                    Provider = p.provider,
                    OrderCode = p.order_code,
                    AmountVnd = p.amount_vnd,
                    GrantedValue = p.chars_granted,
                    GrantedUnit = "chars",
                    Status = p.status,
                    CreatedAt = p.created_at
                });

            var subscriptionQuery = _db.subscription_payments
                .AsNoTracking()
                .Where(p => p.account_id == accountId)
                .Select(p => new PaymentHistoryRecord
                {
                    PaymentId = p.payment_id,
                    Type = TypeSubscription,
                    Provider = p.provider,
                    OrderCode = p.order_code,
                    AmountVnd = p.amount_vnd,
                    GrantedValue = null,
                    GrantedUnit = null,
                    Status = p.status,
                    CreatedAt = p.created_at,
                    PlanCode = p.plan_code,
                    PlanName = p.plan.plan_name
                });

            var combined = diaQuery
                .Concat(voiceQuery)
                .Concat(subscriptionQuery);

            if (!string.IsNullOrWhiteSpace(type))
            {
                combined = combined.Where(r => r.Type == type);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                combined = combined.Where(r => r.Status == status);
            }

            if (from.HasValue)
            {
                combined = combined.Where(r => r.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                combined = combined.Where(r => r.CreatedAt <= to.Value);
            }

            var total = await combined.CountAsync(ct);
            var skip = (page - 1) * pageSize;

            var items = await combined
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.PaymentId)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }
    }
}

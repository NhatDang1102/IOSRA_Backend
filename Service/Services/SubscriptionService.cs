using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Subscription;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Repository.DBContext;
using Repository.Entities;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(AppDbContext db, ILogger<SubscriptionService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IReadOnlyList<SubscriptionPlanResponse>> GetPlansAsync(CancellationToken ct = default)
        {
            var plans = await _db.subscription_plans
                .AsNoTracking()
                .OrderBy(p => p.price_vnd)
                .Select(p => new SubscriptionPlanResponse
                {
                    PlanCode = p.plan_code,
                    PlanName = p.plan_name,
                    PriceVnd = p.price_vnd,
                    DurationDays = p.duration_days,
                    DailyDias = p.daily_dias
                })
                .ToListAsync(ct);

            return plans;
        }

        public async Task<SubscriptionStatusResponse> GetStatusAsync(Guid accountId, CancellationToken ct = default)
        {
            var now = TimezoneConverter.VietnamNow;
            var subscription = await _db.subcriptions
                .Include(s => s.plan_codeNavigation)
                .Where(s => s.user_id == accountId && s.end_at >= now)
                .OrderByDescending(s => s.end_at)
                .FirstOrDefaultAsync(ct);

            if (subscription == null)
            {
                return new SubscriptionStatusResponse
                {
                    HasActiveSubscription = false,
                    CanClaimToday = false
                };
            }

            var plan = subscription.plan_codeNavigation;
            var today = DateOnly.FromDateTime(now);
            DateTime? lastClaimAt = subscription.last_claim_date?.ToDateTime(TimeOnly.MinValue);

            return new SubscriptionStatusResponse
            {
                HasActiveSubscription = true,
                PlanCode = subscription.plan_code,
                PlanName = plan.plan_name,
                StartAt = subscription.start_at,
                EndAt = subscription.end_at,
                DailyDias = plan.daily_dias,
                PriceVnd = plan.price_vnd,
                LastClaimedAt = lastClaimAt,
                CanClaimToday = !subscription.last_claim_date.HasValue || subscription.last_claim_date.Value != today
            };
        }

        public async Task<SubscriptionClaimResponse> ClaimDailyAsync(Guid accountId, CancellationToken ct = default)
        {
            var now = TimezoneConverter.VietnamNow;
            var subscription = await _db.subcriptions
                .Include(s => s.plan_codeNavigation)
                .FirstOrDefaultAsync(s => s.user_id == accountId && s.end_at >= now, ct);

            if (subscription == null)
            {
                throw new AppException("SubscriptionNotFound", "B?n chua có gói subscription dang ho?t d?ng.", 400);
            }

            var plan = subscription.plan_codeNavigation;
            var today = DateOnly.FromDateTime(now);
            if (subscription.last_claim_date.HasValue && subscription.last_claim_date.Value == today)
            {
                throw new AppException("SubscriptionClaimed", "B?n dã nh?n dias hôm nay r?i.", 400);
            }

            var wallet = await _db.dia_wallets.FirstOrDefaultAsync(w => w.account_id == accountId, ct);
            if (wallet == null)
            {
                wallet = new dia_wallet
                {
                    wallet_id = Guid.NewGuid(),
                    account_id = accountId,
                    balance_coin = 0,
                    locked_coin = 0,
                    updated_at = now
                };
                _db.dia_wallets.Add(wallet);
            }

            wallet.balance_coin += plan.daily_dias;
            wallet.updated_at = now;

            var walletPayment = new wallet_payment
            {
                trs_id = Guid.NewGuid(),
                wallet_id = wallet.wallet_id,
                type = "adjust",
                coin_delta = (long)plan.daily_dias,
                coin_after = wallet.balance_coin,
                ref_id = subscription.sub_id,
                created_at = now
            };
            _db.wallet_payments.Add(walletPayment);

            subscription.last_claim_date = today;
            subscription.claimed_today = true;

            await _db.SaveChangesAsync(ct);

            return new SubscriptionClaimResponse
            {
                SubscriptionId = subscription.sub_id,
                ClaimedDias = plan.daily_dias,
                WalletBalance = wallet.balance_coin,
                ClaimedAt = now,
                NextClaimAvailableAt = today.AddDays(1).ToDateTime(TimeOnly.MinValue)
            };
        }

        public async Task ActivateSubscriptionAsync(Guid accountId, string planCode, CancellationToken ct = default)
        {
            var plan = await _db.subscription_plans.FirstOrDefaultAsync(p => p.plan_code == planCode, ct)
                       ?? throw new AppException("SubscriptionPlanNotFound", "Gói subscription không t?n t?i.", 404);

            var now = TimezoneConverter.VietnamNow;
            var subscription = await _db.subcriptions.FirstOrDefaultAsync(s => s.user_id == accountId && s.plan_code == planCode, ct);

            if (subscription == null)
            {
                subscription = new subcription
                {
                    sub_id = Guid.NewGuid(),
                    user_id = accountId,
                    plan_code = planCode,
                    start_at = now,
                    end_at = now.AddDays(plan.duration_days),
                    last_claim_date = null,
                    claimed_today = false
                };
                _db.subcriptions.Add(subscription);
            }
            else
            {
                if (subscription.end_at < now)
                {
                    subscription.start_at = now;
                    subscription.end_at = now.AddDays(plan.duration_days);
                    subscription.last_claim_date = null;
                }
                else
                {
                    subscription.end_at = subscription.end_at.AddDays(plan.duration_days);
                }

                subscription.claimed_today = false;
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}





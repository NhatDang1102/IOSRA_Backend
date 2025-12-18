using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Subscription;
using Microsoft.Extensions.Logging;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly ISubscriptionRepository _repository;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(ISubscriptionRepository repository, ILogger<SubscriptionService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        //lấy hết plan trong db
        public async Task<IReadOnlyList<SubscriptionPlanResponse>> GetPlansAsync(CancellationToken ct = default)
        {
            var plans = await _repository.GetPlansAsync(ct);

            return plans
                .Select(p => new SubscriptionPlanResponse
                {
                    PlanCode = p.plan_code,
                    PlanName = p.plan_name,
                    PriceVnd = p.price_vnd,
                    DurationDays = p.duration_days,
                    DailyDias = p.daily_dias
                })
                .ToList();
        }
        //check status subscrtion của reader 
        public async Task<SubscriptionStatusResponse> GetStatusAsync(Guid accountId, CancellationToken ct = default)
        {
            var now = TimezoneConverter.VietnamNow;
            var subscription = await _repository.GetLatestActiveAsync(accountId, now, ct);

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
        //check hnay đã nhận dias chưa và nếu chưa thì cho nhận
        public async Task<SubscriptionClaimResponse> ClaimDailyAsync(Guid accountId, CancellationToken ct = default)
        {
            var now = TimezoneConverter.VietnamNow;
            var subscription = await _repository.GetLatestActiveAsync(accountId, now, ct);

            if (subscription == null)
            {
                throw new AppException("SubscriptionNotFound", "Bạn chưa có gói nào.", 400);
            }

            var plan = subscription.plan_codeNavigation;
            var today = DateOnly.FromDateTime(now);
            if (subscription.last_claim_date.HasValue && subscription.last_claim_date.Value == today)
            {
                throw new AppException("SubscriptionClaimed", "Bạn đã nhận kc hnay rồi.", 400);
            }

            var wallet = await _repository.GetWalletAsync(accountId, ct);
            if (wallet == null)
            {
                wallet = new dia_wallet
                {
                    wallet_id = Guid.NewGuid(),
                    account_id = accountId,
                    balance_dias = 0,
                    locked_dias = 0,                    
                    updated_at = now
                };
                await _repository.AddWalletAsync(wallet, ct);
            }

                                wallet.balance_dias += plan.daily_dias;            wallet.updated_at = now;

            var walletPayment = new wallet_payment
            {
                trs_id = Guid.NewGuid(),
                wallet_id = wallet.wallet_id,
                type = "adjust",
                                        dias_delta = (long)plan.daily_dias,
                                        dias_after = wallet.balance_dias,               
                ref_id = subscription.sub_id,
                created_at = now
            };
            _repository.AddWalletPayment(walletPayment);

            subscription.last_claim_date = today;
            subscription.claimed_today = true;

            await _repository.SaveChangesAsync(ct);

            return new SubscriptionClaimResponse
            {
                SubscriptionId = subscription.sub_id,
                ClaimedDias = plan.daily_dias,
                WalletBalance = wallet.balance_dias,
                ClaimedAt = now,
                NextClaimAvailableAt = today.AddDays(1).ToDateTime(TimeOnly.MinValue)
            };
        }

        public async Task ActivateSubscriptionAsync(Guid accountId, string planCode, CancellationToken ct = default)
        {
            var plan = await _repository.GetPlanAsync(planCode, ct)
                       ?? throw new AppException("SubscriptionPlanNotFound", "Gói subscription không tồn tại.", 404);

            var now = TimezoneConverter.VietnamNow;
            var subscription = await _repository.GetByPlanAsync(accountId, planCode, ct);

            if (subscription == null)
            {
                subscription = new subscription
                {
                    sub_id = Guid.NewGuid(),
                    user_id = accountId,
                    plan_code = planCode,
                    start_at = now,
                    end_at = now.AddDays(plan.duration_days),
                    last_claim_date = null,
                    claimed_today = false
                };
                await _repository.AddSubscriptionAsync(subscription, ct);
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

            await _repository.SaveChangesAsync(ct);

            // Grant initial diamonds if configured for the plan
            if (plan.initial_dias > 0)
            {
                var wallet = await _repository.GetWalletAsync(accountId, ct);
                if (wallet == null)
                {
                    wallet = new dia_wallet
                    {
                        wallet_id = Guid.NewGuid(),
                        account_id = accountId,
                        balance_dias = 0,
                        locked_dias = 0,
                        updated_at = now
                    };
                    await _repository.AddWalletAsync(wallet, ct);
                }

                wallet.balance_dias += plan.initial_dias;
                wallet.updated_at = now;

                var walletPayment = new wallet_payment
                {
                    trs_id = Guid.NewGuid(),
                    wallet_id = wallet.wallet_id,
                    type = "adjust",
                    dias_delta = (long)plan.initial_dias,
                    dias_after = wallet.balance_dias,
                    ref_id = subscription.sub_id, // Link to subscription
                    created_at = now
                };
                _repository.AddWalletPayment(walletPayment);

                await _repository.SaveChangesAsync(ct);
            }
        }
    }
}





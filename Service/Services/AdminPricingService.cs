using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class AdminPricingService : IAdminPricingService
    {
        private readonly IBillingRepository _billingRepository;

        public AdminPricingService(IBillingRepository billingRepository)
        {
            _billingRepository = billingRepository;
        }

        public Task<List<topup_pricing>> GetAllTopupPricingsAsync(CancellationToken ct = default)
        {
            return _billingRepository.GetAllTopupPricingsAsync(ct);
        }

        public async Task UpdateTopupPricingAsync(UpdateTopupPricingRequest request, CancellationToken ct = default)
        {
            var existing = await _billingRepository.GetTopupPricingByIdAsync(request.PricingId, ct);
            if (existing == null) throw new AppException("NotFound", "Không tìm thấy bảng giá", 404);

            existing.diamond_granted = request.DiamondGranted;

            await _billingRepository.UpdateTopupPricingAsync(existing, ct);
            await _billingRepository.SaveChangesAsync(ct);
        }

        public Task<List<subscription_plan>> GetAllSubscriptionPlansAsync(CancellationToken ct = default)
        {
            return _billingRepository.GetAllSubscriptionPlansAsync(ct);
        }

        public async Task UpdateSubscriptionPlanAsync(UpdateSubscriptionPlanRequest request, CancellationToken ct = default)
        {
            var existing = await _billingRepository.GetSubscriptionPlanAsync(request.PlanCode, ct);
            if (existing == null) throw new AppException("NotFound", "Không tìm thấy gói đăng ký", 404);

            existing.price_vnd = request.PriceVnd;
            existing.daily_dias = request.DailyDias;

            await _billingRepository.UpdateSubscriptionPlanAsync(existing, ct);
            await _billingRepository.SaveChangesAsync(ct);
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize(Roles = "admin")]
    [Route("api/[controller]")]
    public class AdminPricingController : AppControllerBase
    {
        private readonly IChapterPricingService _chapterPricingService;
        private readonly IVoicePricingService _voicePricingService;
        private readonly IAdminPricingService _adminPricingService;

        public AdminPricingController(
            IChapterPricingService chapterPricingService,
            IVoicePricingService voicePricingService,
            IAdminPricingService adminPricingService)
        {
            _chapterPricingService = chapterPricingService;
            _voicePricingService = voicePricingService;
            _adminPricingService = adminPricingService;
        }

        [HttpGet("chapter-rules")]
        public async Task<IActionResult> GetChapterRules(CancellationToken ct)
        {
            var rules = await _chapterPricingService.GetAllRulesAsync(ct);
            return Ok(rules);
        }

        [HttpPut("chapter-rules")]
        public async Task<IActionResult> UpdateChapterRule([FromBody] UpdateChapterPriceRuleRequest request, CancellationToken ct)
        {
            await _chapterPricingService.UpdateRuleAsync(request, ct);
            return Ok(new { message = "Chapter pricing rule updated successfully" });
        }

        [HttpGet("voice-rules")]
        public async Task<IActionResult> GetVoiceRules(CancellationToken ct)
        {
            var rules = await _voicePricingService.GetRawRulesAsync(ct);
            return Ok(rules);
        }

        [HttpPut("voice-rules")]
        public async Task<IActionResult> UpdateVoiceRule([FromBody] UpdateVoicePriceRuleRequest request, CancellationToken ct)
        {
            await _voicePricingService.UpdateRuleAsync(request, ct);
            return Ok(new { message = "Voice pricing rule updated successfully" });
        }

        [HttpGet("topup-pricing")]
        public async Task<IActionResult> GetTopupPricings(CancellationToken ct)
        {
            var result = await _adminPricingService.GetAllTopupPricingsAsync(ct);
            return Ok(result);
        }

        [HttpPut("topup-pricing")]
        public async Task<IActionResult> UpdateTopupPricing([FromBody] UpdateTopupPricingRequest request, CancellationToken ct)
        {
            await _adminPricingService.UpdateTopupPricingAsync(request, ct);
            return Ok(new { message = "Topup pricing updated successfully" });
        }

        [HttpGet("subscription-plans")]
        public async Task<IActionResult> GetSubscriptionPlans(CancellationToken ct)
        {
            var result = await _adminPricingService.GetAllSubscriptionPlansAsync(ct);
            return Ok(result);
        }

        [HttpPut("subscription-plans")]
        public async Task<IActionResult> UpdateSubscriptionPlan([FromBody] UpdateSubscriptionPlanRequest request, CancellationToken ct)
        {
            await _adminPricingService.UpdateSubscriptionPlanAsync(request, ct);
            return Ok(new { message = "Subscription plan updated successfully" });
        }
    }
}

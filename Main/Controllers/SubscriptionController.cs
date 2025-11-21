using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Subscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubscriptionController : AppControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpGet("plans")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPlans(CancellationToken ct = default)
        {
            var plans = await _subscriptionService.GetPlansAsync(ct);
            return Ok(plans);
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(CancellationToken ct = default)
        {
            var status = await _subscriptionService.GetStatusAsync(AccountId, ct);
            return Ok(status);
        }

        [HttpPost("claim-daily")]
        public async Task<IActionResult> ClaimDaily(CancellationToken ct = default)
        {
            var response = await _subscriptionService.ClaimDailyAsync(AccountId, ct);
            return Ok(response);
        }
    }
}

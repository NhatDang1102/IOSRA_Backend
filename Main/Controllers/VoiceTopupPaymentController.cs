using Contract.DTOs.Request.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "author,AUTHOR")]
    public class VoiceTopupPaymentController : AppControllerBase
    {
        private readonly IVoicePaymentService _voicePaymentService;
        private readonly ILogger<VoiceTopupPaymentController> _logger;

        public VoiceTopupPaymentController(IVoicePaymentService voicePaymentService, ILogger<VoiceTopupPaymentController> logger)
        {
            _voicePaymentService = voicePaymentService;
            _logger = logger;
        }

        [HttpGet("pricing")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVoicePricing(CancellationToken ct)
        {
            var packages = await _voicePaymentService.GetVoiceTopupPricingsAsync(ct);
            return Ok(packages);
        }

        [HttpPost("create-link")]
        public async Task<IActionResult> CreateLink([FromBody] CreateVoicePaymentLinkRequest request, CancellationToken ct)
        {
            try
            {
                var response = await _voicePaymentService.CreatePaymentLinkAsync(AccountId, request.Amount, ct);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating voice payment link for account {AccountId}", AccountId);
                return StatusCode(500, new { message = "An error occurred while creating payment link" });
            }
        }

        [HttpPost("cancel-link")]
        public async Task<IActionResult> CancelLink([FromBody] CancelPaymentRequest request, CancellationToken ct)
        {
            var success = await _voicePaymentService.CancelPaymentLinkAsync(request.TransactionId, request.CancellationReason, ct);
            if (success)
            {
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false });
        }
    }
}

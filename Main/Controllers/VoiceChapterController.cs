using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Voice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class VoiceChapterController : AppControllerBase
    {
        private readonly IVoiceChapterService _voiceChapterService;
        private readonly IVoicePricingService _voicePricingService;

        public VoiceChapterController(IVoiceChapterService voiceChapterService, IVoicePricingService voicePricingService)
        {
            _voiceChapterService = voiceChapterService;
            _voicePricingService = voicePricingService;
        }

        [HttpGet("pricing-rules")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPricingRules(CancellationToken ct = default)
        {
            var rules = await _voicePricingService.GetAllRulesAsync(ct);
            return Ok(rules);
        }

        [HttpGet("{chapterId:guid}")]
        public async Task<IActionResult> GetStatus(Guid chapterId, CancellationToken ct = default)
        {
            var response = await _voiceChapterService.GetAsync(AccountId, chapterId, ct);
            return Ok(response);
        }

        [HttpPost("{chapterId:guid}/order")]
        public async Task<IActionResult> Order(Guid chapterId, [FromBody] VoiceChapterOrderRequest request, CancellationToken ct = default)
        {
            var response = await _voiceChapterService.OrderVoicesAsync(AccountId, chapterId, request, ct);
            return Ok(response);
        }

        [HttpGet("{chapterId:guid}/char-count")]
        public async Task<IActionResult> GetCharCount(Guid chapterId, CancellationToken ct = default)
        {
            var response = await _voiceChapterService.GetCharCountAsync(AccountId, chapterId, ct);
            return Ok(response);
        }

        [HttpGet("voice-list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVoiceList(CancellationToken ct = default)
        {
            var response = await _voiceChapterService.GetPresetsAsync(ct);
            return Ok(response);
        }

    }
}

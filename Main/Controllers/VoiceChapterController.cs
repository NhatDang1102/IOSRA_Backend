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

        public VoiceChapterController(IVoiceChapterService voiceChapterService)
        {
            _voiceChapterService = voiceChapterService;
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

    }
}

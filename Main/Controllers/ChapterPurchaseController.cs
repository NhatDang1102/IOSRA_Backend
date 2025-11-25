using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class ChapterPurchaseController : AppControllerBase
    {
        private readonly IChapterPurchaseService _chapterPurchaseService;

        public ChapterPurchaseController(IChapterPurchaseService chapterPurchaseService)
        {
            _chapterPurchaseService = chapterPurchaseService;
        }

        [HttpPost("{chapterId:guid}")]
        public async Task<ActionResult<ChapterPurchaseResponse>> Purchase(Guid chapterId, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.PurchaseAsync(AccountId, chapterId, ct);
            return Ok(result);
        }

        [HttpPost("{chapterId:guid}/order-voice")]
        public async Task<ActionResult<ChapterVoicePurchaseResponse>> PurchaseVoices(Guid chapterId, [FromBody] ChapterVoicePurchaseRequest request, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.PurchaseVoicesAsync(AccountId, chapterId, request, ct);
            return Ok(result);
        }

        [HttpGet("chapter-history")]
        public async Task<ActionResult<IReadOnlyList<PurchasedChapterResponse>>> GetChapterHistory([FromQuery] Guid? storyId, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.GetPurchasedChaptersAsync(AccountId, storyId, ct);
            return Ok(result);
        }

        [HttpGet("{chapterId:guid}/voice-history")]
        public async Task<ActionResult<IReadOnlyList<PurchasedVoiceResponse>>> GetPurchasedVoices(Guid chapterId, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.GetPurchasedVoicesAsync(AccountId, chapterId, ct);
            return Ok(result);
        }

        [HttpGet("voice-history")]
        public async Task<ActionResult<IReadOnlyList<PurchasedVoiceHistoryResponse>>> GetVoiceHistory(CancellationToken ct)
        {
            var result = await _chapterPurchaseService.GetPurchasedVoiceHistoryAsync(AccountId, ct);
            return Ok(result);
        }
    }
}

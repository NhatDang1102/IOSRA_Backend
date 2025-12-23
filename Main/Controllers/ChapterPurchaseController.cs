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
    [Authorize] // Bắt buộc đăng nhập cho toàn bộ Controller này
    [Route("api/[controller]")]
    public class ChapterPurchaseController : AppControllerBase
    {
        private readonly IChapterPurchaseService _chapterPurchaseService;

        public ChapterPurchaseController(IChapterPurchaseService chapterPurchaseService)
        {
            _chapterPurchaseService = chapterPurchaseService;
        }

        // API Mua nội dung văn bản của một chương (Trừ Dias)
        [HttpPost("{chapterId:guid}")]
        public async Task<ActionResult<ChapterPurchaseResponse>> Purchase(Guid chapterId, CancellationToken ct)
        {
            // AccountId được lấy từ Token (BaseController)
            var result = await _chapterPurchaseService.PurchaseAsync(AccountId, chapterId, ct);
            return Ok(result);
        }

        // API Mua giọng đọc (Voice) cho một chương (có thể chọn nhiều giọng cùng lúc)
        [HttpPost("{chapterId:guid}/order-voice")]
        public async Task<ActionResult<ChapterVoicePurchaseResponse>> PurchaseVoices(Guid chapterId, [FromBody] ChapterVoicePurchaseRequest request, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.PurchaseVoicesAsync(AccountId, chapterId, request, ct);
            return Ok(result);
        }

        // API Lấy lịch sử mua chương (Có thể lọc theo StoryId)
        [HttpGet("chapter-history")]
        public async Task<ActionResult<IReadOnlyList<PurchasedChapterResponse>>> GetChapterHistory([FromQuery] Guid? storyId, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.GetPurchasedChaptersAsync(AccountId, storyId, ct);
            return Ok(result);
        }

        // API Lấy danh sách giọng đọc đã mua của một chương cụ thể
        [HttpGet("{chapterId:guid}/voice-history")]
        public async Task<ActionResult<IReadOnlyList<PurchasedVoiceResponse>>> GetPurchasedVoices(Guid chapterId, CancellationToken ct)
        {
            var result = await _chapterPurchaseService.GetPurchasedVoicesAsync(AccountId, chapterId, ct);
            return Ok(result);
        }

        // API Lấy toàn bộ lịch sử mua giọng đọc (được group theo truyện/chương)
        [HttpGet("voice-history")]
        public async Task<ActionResult<IReadOnlyList<PurchasedVoiceHistoryResponse>>> GetVoiceHistory(CancellationToken ct)
        {
            var result = await _chapterPurchaseService.GetPurchasedVoiceHistoryAsync(AccountId, ct);
            return Ok(result);
        }
    }
}
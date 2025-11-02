using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/moderation/chapters")]
    [Authorize(Roles = "cmod,CMOD,CONTENT_MODERATOR,content_moderator,admin,ADMIN")]
    public class ChapterModerationController : AppControllerBase
    {
        private readonly IChapterModerationService _chapterModerationService;

        public ChapterModerationController(IChapterModerationService chapterModerationService)
        {
            _chapterModerationService = chapterModerationService;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        {
            var items = await _chapterModerationService.ListAsync(status, ct);
            return Ok(items);
        }

        [HttpGet("{reviewId:guid}")]
        public async Task<IActionResult> Get([FromRoute] Guid reviewId, CancellationToken ct)
        {
            var item = await _chapterModerationService.GetAsync(reviewId, ct);
            return Ok(item);
        }

        [HttpPost("{reviewId:guid}/decision")]
        public async Task<IActionResult> Decide([FromRoute] Guid reviewId, [FromBody] ChapterModerationDecisionRequest request, CancellationToken ct)
        {
            await _chapterModerationService.ModerateAsync(AccountId, reviewId, request, ct);
            return Ok(new { message = request.Approve ? "Chapter approved." : "Chapter rejected." });
        }
    }
}

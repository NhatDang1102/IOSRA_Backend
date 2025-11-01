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

        [HttpGet("pending")]
        public async Task<IActionResult> ListPending(CancellationToken ct)
        {
            var pending = await _chapterModerationService.ListPendingAsync(ct);
            return Ok(pending);
        }

        [HttpPost("{chapterId:guid}/decision")]
        public async Task<IActionResult> Decide([FromRoute] Guid chapterId, [FromBody] ChapterModerationDecisionRequest request, CancellationToken ct)
        {
            await _chapterModerationService.ModerateAsync(AccountId, chapterId, request, ct);
            return Ok(new { message = request.Approve ? "Chapter approved." : "Chapter rejected." });
        }
    }
}

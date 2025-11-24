using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/moderation/comments")]
    [Authorize(Roles = "cmod,CMOD,CONTENT_MODERATOR,content_moderator")]
    public class ChapterCommentModerationController : AppControllerBase
    {
        private readonly IChapterCommentService _chapterCommentService;

        public ChapterCommentModerationController(IChapterCommentService chapterCommentService)
        {
            _chapterCommentService = chapterCommentService;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] string? status,
            [FromQuery] Guid? storyId,
            [FromQuery] Guid? chapterId,
            [FromQuery] Guid? readerId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            var result = await _chapterCommentService.GetForModerationAsync(status, storyId, chapterId, readerId, page, pageSize, ct);
            return Ok(result);
        }
    }
}

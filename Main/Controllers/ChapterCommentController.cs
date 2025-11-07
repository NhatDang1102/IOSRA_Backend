using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    public class ChapterCommentController : AppControllerBase
    {
        private readonly IChapterCommentService _chapterCommentService;

        public ChapterCommentController(IChapterCommentService chapterCommentService)
        {
            _chapterCommentService = chapterCommentService;
        }

        [HttpGet("chapter/{chapterId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByChapter(Guid chapterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _chapterCommentService.GetByChapterAsync(chapterId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("story/{storyId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByStory(Guid storyId, [FromQuery] Guid? chapterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _chapterCommentService.GetByStoryAsync(storyId, chapterId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("{chapterId:guid}")]
        [Authorize]
        public async Task<IActionResult> Create(Guid chapterId, [FromBody] ChapterCommentCreateRequest request, CancellationToken ct = default)
        {
            var comment = await _chapterCommentService.CreateAsync(AccountId, chapterId, request, ct);
            return Ok(comment);
        }

        [HttpPut("{chapterId:guid}/{commentId:guid}")]
        [Authorize]
        public async Task<IActionResult> Update(Guid chapterId, Guid commentId, [FromBody] ChapterCommentUpdateRequest request, CancellationToken ct = default)
        {
            var comment = await _chapterCommentService.UpdateAsync(AccountId, chapterId, commentId, request, ct);
            return Ok(comment);
        }

        [HttpDelete("{chapterId:guid}/{commentId:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid chapterId, Guid commentId, CancellationToken ct = default)
        {
            await _chapterCommentService.DeleteAsync(AccountId, chapterId, commentId, ct);
            return NoContent();
        }
    }
}

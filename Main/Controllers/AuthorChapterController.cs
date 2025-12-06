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
    [Authorize(Roles = "author,AUTHOR")]
    public class AuthorChapterController : AppControllerBase
    {
        private readonly IAuthorChapterService _authorChapterService;

        public AuthorChapterController(IAuthorChapterService authorChapterService)
        {
            _authorChapterService = authorChapterService;
        }

        [HttpGet("{storyId:guid}")]
        public async Task<IActionResult> GetAll([FromRoute] Guid storyId, [FromQuery] string? status, CancellationToken ct)
        {
            var chapters = await _authorChapterService.GetAllAsync(AccountId, storyId, status, ct);
            return Ok(chapters);
        }

        [HttpGet("{storyId:guid}/{chapterId:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid storyId, [FromRoute] Guid chapterId, CancellationToken ct)
        {
            var chapter = await _authorChapterService.GetByIdAsync(AccountId, storyId, chapterId, ct);
            return Ok(chapter);
        }

        [HttpPost("{storyId:guid}")]
        public async Task<IActionResult> Create([FromRoute] Guid storyId, [FromBody] ChapterCreateRequest request, CancellationToken ct)
        {
            var chapter = await _authorChapterService.CreateAsync(AccountId, storyId, request, ct);
            return Ok(chapter);
        }

        [HttpPut("{storyId:guid}/{chapterId:guid}")]
        public async Task<IActionResult> UpdateDraft([FromRoute] Guid storyId, [FromRoute] Guid chapterId, [FromBody] ChapterUpdateRequest request, CancellationToken ct)
        {
            var chapter = await _authorChapterService.UpdateDraftAsync(AccountId, storyId, chapterId, request, ct);
            return Ok(chapter);
        }

        [HttpPost("{chapterId:guid}/submit")]
        public async Task<IActionResult> SubmitForReview([FromRoute] Guid chapterId, [FromBody] ChapterSubmitRequest request, CancellationToken ct)
        {
            var chapter = await _authorChapterService.SubmitAsync(AccountId, chapterId, request, ct);
            return Ok(chapter);
        }

        [HttpPost("{chapterId:guid}/withdraw")]
        public async Task<IActionResult> WithdrawChapter([FromRoute] Guid chapterId, CancellationToken ct)
        {
            var chapter = await _authorChapterService.WithdrawAsync(AccountId, chapterId, ct);
            return Ok(chapter);
        }
    }
}

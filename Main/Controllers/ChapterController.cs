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
    public class ChapterController : AppControllerBase
    {
        private readonly IChapterService _chapterService;

        public ChapterController(IChapterService chapterService)
        {
            _chapterService = chapterService;
        }

        [HttpGet("{storyId:guid}")]
        public async Task<IActionResult> List([FromRoute] Guid storyId, [FromQuery] string? status, CancellationToken ct)
        {
            var chapters = await _chapterService.ListAsync(AccountId, storyId, status, ct);
            return Ok(chapters);
        }

        [HttpGet("{storyId:guid}/{chapterId:guid}")]
        public async Task<IActionResult> Get([FromRoute] Guid storyId, [FromRoute] Guid chapterId, CancellationToken ct)
        {
            var chapter = await _chapterService.GetAsync(AccountId, storyId, chapterId, ct);
            return Ok(chapter);
        }

        [HttpPost("{storyId:guid}")]
        public async Task<IActionResult> Create([FromRoute] Guid storyId, [FromBody] ChapterCreateRequest request, CancellationToken ct)
        {
            var chapter = await _chapterService.CreateAsync(AccountId, storyId, request, ct);
            return Ok(chapter);
        }

        [HttpPost("{chapterId:guid}/submit")]
        public async Task<IActionResult> Submit([FromRoute] Guid chapterId, [FromBody] ChapterSubmitRequest request, CancellationToken ct)
        {
            var chapter = await _chapterService.SubmitAsync(AccountId, chapterId, request, ct);
            return Ok(chapter);
        }
    }
}

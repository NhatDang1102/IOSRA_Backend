using Contract.DTOs.Request.Chapter;
using System.Threading;
using System.Threading.Tasks;
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

        [HttpGet("{storyId}")]
        public async Task<IActionResult> List([FromRoute] ulong storyId, CancellationToken ct)
        {
            var chapters = await _chapterService.ListAsync(AccountId, storyId, ct);
            return Ok(chapters);
        }

        [HttpGet("{storyId}/{chapterId}")]
        public async Task<IActionResult> Get([FromRoute] ulong storyId, [FromRoute] ulong chapterId, CancellationToken ct)
        {
            var chapter = await _chapterService.GetAsync(AccountId, storyId, chapterId, ct);
            return Ok(chapter);
        }

        [HttpPost("{storyId}")]
        public async Task<IActionResult> Create([FromRoute] ulong storyId, [FromBody] ChapterCreateRequest request, CancellationToken ct)
        {
            var chapter = await _chapterService.CreateAsync(AccountId, storyId, request, ct);
            return Ok(chapter);
        }

        [HttpPost("{storyId}/{chapterId}/submit")]
        public Task<IActionResult> SubmitWithStory([FromRoute] ulong storyId, [FromRoute] ulong chapterId, [FromBody] ChapterSubmitRequest request, CancellationToken ct)
            => SubmitInternalAsync(chapterId, request, ct);

        [HttpPost("submit/{chapterId}")]
        public Task<IActionResult> Submit([FromRoute] ulong chapterId, [FromBody] ChapterSubmitRequest request, CancellationToken ct)
            => SubmitInternalAsync(chapterId, request, ct);

        private async Task<IActionResult> SubmitInternalAsync(ulong chapterId, ChapterSubmitRequest request, CancellationToken ct)
        {
            var chapter = await _chapterService.SubmitAsync(AccountId, chapterId, request, ct);
            return Ok(chapter);
        }
    }
}

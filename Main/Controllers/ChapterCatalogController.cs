using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Exceptions;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/ChapterCatalog")]
    [AllowAnonymous]
    public class ChapterCatalogController : AppControllerBase
    {
        private readonly IChapterCatalogService _chapterCatalogService;
        private readonly IStoryViewTracker _storyViewTracker;

        public ChapterCatalogController(IChapterCatalogService chapterCatalogService, IStoryViewTracker storyViewTracker)
        {
            _chapterCatalogService = chapterCatalogService;
            _storyViewTracker = storyViewTracker;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<ChapterCatalogListItemResponse>>> List([FromQuery] ChapterCatalogQuery query, CancellationToken ct)
        {
            if (query.StoryId == Guid.Empty)
            {
                throw new AppException("ValidationFailed", "storyId is required.", 400);
            }

            var chapters = await _chapterCatalogService.GetChaptersAsync(query, ct);
            return Ok(chapters);
        }

        [HttpGet("{chapterId:guid}")]
        public async Task<ActionResult<ChapterCatalogDetailResponse>> Get(Guid chapterId, CancellationToken ct)
        {
            var viewerAccountId = TryGetAccountId();
            var chapter = await _chapterCatalogService.GetChapterAsync(chapterId, ct, viewerAccountId);

            var fingerprint = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _storyViewTracker.RecordViewAsync(chapter.StoryId, viewerAccountId, fingerprint, ct);

            return Ok(chapter);
        }

        [HttpGet("{chapterId:guid}/voices")]
        public async Task<ActionResult<IReadOnlyList<ChapterCatalogVoiceResponse>>> GetVoices(Guid chapterId, CancellationToken ct)
        {
            var result = await _chapterCatalogService.GetChapterVoicesAsync(chapterId, TryGetAccountId(), ct);
            return Ok(result);
        }

        [HttpGet("{chapterId:guid}/voices/{voiceId:guid}")]
        public async Task<ActionResult<ChapterCatalogVoiceResponse>> GetVoice(Guid chapterId, Guid voiceId, CancellationToken ct)
        {
            var result = await _chapterCatalogService.GetChapterVoiceAsync(chapterId, voiceId, TryGetAccountId(), ct);
            return Ok(result);
        }
    }
}

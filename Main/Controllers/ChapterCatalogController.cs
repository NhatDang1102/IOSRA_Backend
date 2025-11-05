using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Contract.DTOs.Respond.Common;
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

        public ChapterCatalogController(IChapterCatalogService chapterCatalogService)
        {
            _chapterCatalogService = chapterCatalogService;
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
            var chapter = await _chapterCatalogService.GetChapterAsync(chapterId, ct);
            return Ok(chapter);
        }
    }
}


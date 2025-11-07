using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Common;
using Contract.DTOs.Respond.Story;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    // Controller public để xem danh sách truyện (không cần đăng nhập)
    [Route("api/StoryCatalog")]
    [AllowAnonymous]
    public class StoryCatalogController : AppControllerBase
    {
        private readonly IStoryCatalogService _storyCatalogService;
        private readonly IStoryHighlightService _storyHighlightService;

        public StoryCatalogController(IStoryCatalogService storyCatalogService, IStoryHighlightService storyHighlightService)
        {
            _storyCatalogService = storyCatalogService;
            _storyHighlightService = storyHighlightService;
        }

        // API lấy danh sách truyện với filter: query search, tag, author
        // Bind từng parameter riêng lẻ để tránh issue với complex model binding
        [HttpGet]
        public async Task<ActionResult<PagedResult<StoryCatalogListItemResponse>>> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? query = null,
            [FromQuery] Guid? tagId = null,
            [FromQuery] Guid? authorId = null,
            CancellationToken ct = default)
        {
            var queryObj = new StoryCatalogQuery
            {
                Page = page,
                PageSize = pageSize,
                Query = query,
                TagId = tagId,
                AuthorId = authorId
            };

            var result = await _storyCatalogService.GetStoriesAsync(queryObj, ct);
            return Ok(result);
        }

        // API lấy danh sách truyện mới nhất (cached trong Redis)
        [HttpGet("latest")]
        public async Task<ActionResult<IReadOnlyList<StoryCatalogListItemResponse>>> Latest([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var items = await _storyHighlightService.GetLatestStoriesAsync(limit, ct);
            return Ok(items);
        }

        // API lấy top truyện có lượt view cao nhất trong tuần (cached trong Redis)
        [HttpGet("top-weekly")]
        public async Task<ActionResult<IReadOnlyList<StoryWeeklyHighlightResponse>>> TopWeekly([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var items = await _storyHighlightService.GetTopWeeklyStoriesAsync(limit, ct);
            return Ok(items);
        }

        // API lấy chi tiết một truyện theo ID
        [HttpGet("{storyId:guid}")]
        public async Task<ActionResult<StoryCatalogDetailResponse>> Get(Guid storyId, CancellationToken ct)
        {
            var story = await _storyCatalogService.GetStoryAsync(storyId, ct);
            return Ok(story);
        }
    }
}


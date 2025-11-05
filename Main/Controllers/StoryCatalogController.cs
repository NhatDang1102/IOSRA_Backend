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

        [HttpGet]
        public async Task<ActionResult<PagedResult<StoryCatalogListItemResponse>>> List([FromQuery] StoryCatalogQuery query, CancellationToken ct)
        {
            var result = await _storyCatalogService.GetStoriesAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("latest")]
        public async Task<ActionResult<IReadOnlyList<StoryCatalogListItemResponse>>> Latest([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var items = await _storyHighlightService.GetLatestStoriesAsync(limit, ct);
            return Ok(items);
        }

        [HttpGet("top-weekly")]
        public async Task<ActionResult<IReadOnlyList<StoryWeeklyHighlightResponse>>> TopWeekly([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var items = await _storyHighlightService.GetTopWeeklyStoriesAsync(limit, ct);
            return Ok(items);
        }

        [HttpGet("{storyId:guid}")]
        public async Task<ActionResult<StoryCatalogDetailResponse>> Get(Guid storyId, CancellationToken ct)
        {
            var story = await _storyCatalogService.GetStoryAsync(storyId, ct);
            return Ok(story);
        }
    }
}


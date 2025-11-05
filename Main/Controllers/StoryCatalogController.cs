using System;
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

        public StoryCatalogController(IStoryCatalogService storyCatalogService)
        {
            _storyCatalogService = storyCatalogService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<StoryCatalogListItemResponse>>> List([FromQuery] StoryCatalogQuery query, CancellationToken ct)
        {
            var result = await _storyCatalogService.GetStoriesAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("{storyId:guid}")]
        public async Task<ActionResult<StoryCatalogDetailResponse>> Get(Guid storyId, CancellationToken ct)
        {
            var story = await _storyCatalogService.GetStoryAsync(storyId, ct);
            return Ok(story);
        }
    }
}


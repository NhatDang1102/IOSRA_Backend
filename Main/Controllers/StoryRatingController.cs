using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    public class StoryRatingController : AppControllerBase
    {
        private readonly IStoryRatingService _storyRatingService;

        public StoryRatingController(IStoryRatingService storyRatingService)
        {
            _storyRatingService = storyRatingService;
        }

        [HttpGet("{storyId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(Guid storyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var viewerId = TryGetAccountId();
            var result = await _storyRatingService.GetAsync(storyId, viewerId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("{storyId:guid}")]
        [Authorize]
        public async Task<IActionResult> Upsert(Guid storyId, [FromBody] StoryRatingRequest request, CancellationToken ct = default)
        {
            var result = await _storyRatingService.UpsertAsync(AccountId, storyId, request, ct);
            return Ok(result);
        }

        [HttpDelete("{storyId:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid storyId, CancellationToken ct = default)
        {
            await _storyRatingService.RemoveAsync(AccountId, storyId, ct);
            return NoContent();
        }
    }
}

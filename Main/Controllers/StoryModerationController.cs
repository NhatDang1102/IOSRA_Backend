using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/moderation/stories")]
    [Authorize(Roles = "cmod,CMOD,CONTENT_MODERATOR,content_moderator,admin,ADMIN")]
    public class StoryModerationController : AppControllerBase
    {
        private readonly IStoryModerationService _storyModerationService;

        public StoryModerationController(IStoryModerationService storyModerationService)
        {
            _storyModerationService = storyModerationService;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        {
            var items = await _storyModerationService.ListAsync(status, ct);
            return Ok(items);
        }

        [HttpGet("{reviewId:guid}")]
        public async Task<IActionResult> Get([FromRoute] Guid reviewId, CancellationToken ct)
        {
            var item = await _storyModerationService.GetAsync(reviewId, ct);
            return Ok(item);
        }

        [HttpPost("{reviewId:guid}/decision")]
        public async Task<IActionResult> Decide([FromRoute] Guid reviewId, [FromBody] StoryModerationDecisionRequest request, CancellationToken ct)
        {
            await _storyModerationService.ModerateAsync(AccountId, reviewId, request, ct);
            return Ok(new { message = request.Approve ? "Story approved." : "Story rejected." });
        }
    }
}

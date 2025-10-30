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

        [HttpGet("pending")]
        public async Task<IActionResult> ListPending(CancellationToken ct)
        {
            var pending = await _storyModerationService.ListPendingAsync(ct);
            return Ok(pending);
        }

        [HttpPost("{storyId}/decision")]
        public async Task<IActionResult> Decide([FromRoute] ulong storyId, [FromBody] StoryModerationDecisionRequest request, CancellationToken ct)
        {
            await _storyModerationService.ModerateAsync(AccountId, storyId, request, ct);
            return Ok(new { message = request.Approve ? "Story approved." : "Story rejected." });
        }
    }
}


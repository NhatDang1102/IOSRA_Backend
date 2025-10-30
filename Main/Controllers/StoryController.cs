using Contract.DTOs.Request.Story;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "author,AUTHOR")]
    public class StoryController : AppControllerBase
    {
        private readonly IStoryService _storyService;

        public StoryController(IStoryService storyService)
        {
            _storyService = storyService;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            var stories = await _storyService.ListAsync(AccountId, ct);
            return Ok(stories);
        }

        [HttpGet("{storyId}")]
        public async Task<IActionResult> Get([FromRoute] ulong storyId, CancellationToken ct)
        {
            var story = await _storyService.GetAsync(AccountId, storyId, ct);
            return Ok(story);
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Create([FromForm] StoryCreateRequest request, CancellationToken ct)
        {
            var story = await _storyService.CreateAsync(AccountId, request, ct);
            return Ok(story);
        }

        [HttpPost("{storyId}/submit")]
        public async Task<IActionResult> Submit([FromRoute] ulong storyId, [FromBody] StorySubmitRequest request, CancellationToken ct)
        {
            var story = await _storyService.SubmitForReviewAsync(AccountId, storyId, request, ct);
            return Ok(story);
        }

        [HttpPost("{storyId}/complete")]
        public async Task<IActionResult> Complete([FromRoute] ulong storyId, CancellationToken ct)
        {
            var story = await _storyService.CompleteAsync(AccountId, storyId, ct);
            return Ok(story);
        }
    }
}

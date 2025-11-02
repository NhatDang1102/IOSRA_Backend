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
    [Authorize(Roles = "author,AUTHOR")]
    public class StoryController : AppControllerBase
    {
        private readonly IStoryService _storyService;

        public StoryController(IStoryService storyService)
        {
            _storyService = storyService;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        {
            var stories = await _storyService.ListAsync(AccountId, status, ct);
            return Ok(stories);
        }

        [HttpGet("{storyId:guid}")]
        public async Task<IActionResult> Get([FromRoute] Guid storyId, CancellationToken ct)
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

        [HttpPost("{storyId:guid}/submit")]
        public async Task<IActionResult> Submit([FromRoute] Guid storyId, [FromBody] StorySubmitRequest request, CancellationToken ct)
        {
            var story = await _storyService.SubmitForReviewAsync(AccountId, storyId, request, ct);
            return Ok(story);
        }

        [HttpPost("{storyId:guid}/complete")]
        public async Task<IActionResult> Complete([FromRoute] Guid storyId, CancellationToken ct)
        {
            var story = await _storyService.CompleteAsync(AccountId, storyId, ct);
            return Ok(story);
        }
    }
}

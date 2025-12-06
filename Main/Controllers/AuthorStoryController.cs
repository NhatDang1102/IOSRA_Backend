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
    public class AuthorStoryController : AppControllerBase
    {
        private readonly IAuthorStoryService _authorStoryService;

        public AuthorStoryController(IAuthorStoryService authorStoryService)
        {
            _authorStoryService = authorStoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, CancellationToken ct)
        {
            var stories = await _authorStoryService.GetAllAsync(AccountId, status, ct);
            return Ok(stories);
        }

        [HttpGet("{storyId:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid storyId, CancellationToken ct)
        {
            var story = await _authorStoryService.GetByIdAsync(AccountId, storyId, ct);
            return Ok(story);
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> Create([FromForm] StoryCreateRequest request, CancellationToken ct)
        {
            var story = await _authorStoryService.CreateAsync(AccountId, request, ct);
            return Ok(story);
        }

        [HttpPut("{storyId:guid}")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UpdateDraft([FromRoute] Guid storyId, [FromForm] StoryUpdateRequest request, CancellationToken ct)
        {
            var story = await _authorStoryService.UpdateDraftAsync(AccountId, storyId, request, ct);
            return Ok(story);
        }

        [HttpPost("{storyId:guid}/submit")]
        public async Task<IActionResult> SubmitForReview([FromRoute] Guid storyId, [FromBody] StorySubmitRequest request, CancellationToken ct)
        {
            var story = await _authorStoryService.SubmitForReviewAsync(AccountId, storyId, request, ct);
            return Ok(story);
        }

        [HttpPost("{storyId:guid}/complete")]
        public async Task<IActionResult> CompleteStory([FromRoute] Guid storyId, CancellationToken ct)
        {
            var story = await _authorStoryService.CompleteAsync(AccountId, storyId, ct);
            return Ok(story);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestModerationController : ControllerBase
    {
        private readonly IOpenAiModerationService _moderationService;

        public TestModerationController(IOpenAiModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        [HttpPost("story")]
        public async Task<IActionResult> TestStory([FromBody] TestStoryRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Title is required.");

            var result = await _moderationService.ModerateStoryAsync(
                request.Title, 
                request.Description, 
                request.Outline ?? string.Empty, 
                request.LanguageCode ?? "vi-VN", 
                ct);

            return Ok(new
            {
                Score = Math.Round(result.Score, 2),
                Decision = GetDecision(result.Score, result.ShouldReject),
                Explanation = result.Explanation,
                Violations = result.Violations,
                SanitizedContent = result.SanitizedContent
            });
        }

        [HttpPost("chapter")]
        public async Task<IActionResult> TestChapter([FromBody] TestChapterRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Title is required.");
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Content is required.");

            var result = await _moderationService.ModerateChapterAsync(
                request.Title, 
                request.Content, 
                request.LanguageCode ?? "vi-VN", 
                ct);

            return Ok(new
            {
                Score = Math.Round(result.Score, 2),
                Decision = GetDecision(result.Score, result.ShouldReject),
                Explanation = result.Explanation,
                Violations = result.Violations,
                SanitizedContent = result.SanitizedContent
            });
        }

        private static string GetDecision(double score, bool shouldReject)
        {
            if (shouldReject || score < 5.0) return "rejected";
            if (score >= 7.0) return "auto_approved";
            return "pending_manual_review";
        }

        public class TestStoryRequest
        {
            public string Title { get; set; } = null!;
            public string? Description { get; set; }
            public string? Outline { get; set; }
            public string? LanguageCode { get; set; }
        }

        public class TestChapterRequest
        {
            public string Title { get; set; } = null!;
            public string Content { get; set; } = null!;
            public string? LanguageCode { get; set; }
        }
    }
}

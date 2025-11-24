using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize(Roles = "cmod,CONTENT_MOD")]
    [Route("api/[controller]")]
    public class ContentModStatController : AppControllerBase
    {
        private readonly IContentModStatService _statService;

        public ContentModStatController(IContentModStatService statService)
        {
            _statService = statService;
        }

        [HttpGet("stories")]
        public async Task<IActionResult> GetStoryStats([FromQuery] StatQueryRequest query, CancellationToken ct)
        {
            var result = await _statService.GetStoryPublishStatsAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("chapters")]
        public async Task<IActionResult> GetChapterStats([FromQuery] StatQueryRequest query, CancellationToken ct)
        {
            var result = await _statService.GetChapterPublishStatsAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("story-decisions")]
        public async Task<IActionResult> GetStoryDecisionStats([FromQuery] StatQueryRequest query, [FromQuery] string? status, CancellationToken ct)
        {
            var result = await _statService.GetStoryDecisionStatsAsync(status, query, ct);
            return Ok(result);
        }

        [HttpGet("reports")]
        public async Task<IActionResult> GetReportStats([FromQuery] StatQueryRequest query, [FromQuery] string? status, CancellationToken ct)
        {
            var result = await _statService.GetReportStatsAsync(status, query, ct);
            return Ok(result);
        }

        [HttpGet("reports/handled")]
        public async Task<IActionResult> GetHandledReportStats([FromQuery] StatQueryRequest query, [FromQuery] string? status, CancellationToken ct)
        {
            var result = await _statService.GetHandledReportStatsAsync(status, AccountId, query, ct);
            return Ok(result);
        }
    }
}

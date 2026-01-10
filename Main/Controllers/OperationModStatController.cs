using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize(Roles = "omod,OPERATION_MOD")]
    [Route("api/[controller]")]
    public class OperationModStatController : AppControllerBase
    {
        private readonly IOperationModStatService _statService;

        public OperationModStatController(IOperationModStatService statService)
        {
            _statService = statService;
        }

        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenue([FromQuery] StatQueryRequest query, CancellationToken ct)
        {
            if (query.GenerateReport)
            {
                var file = await _statService.ExportRevenueStatsAsync(query, AccountId, ct);
                return File(file.Content, file.ContentType, file.FileName);
            }
            var result = await _statService.GetRevenueStatsAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("requests/{type}")]
        public async Task<IActionResult> GetRequestStats([FromRoute] string type, [FromQuery] StatQueryRequest query, CancellationToken ct)
        {
            if (query.GenerateReport)
            {
                var file = await _statService.ExportRequestStatsAsync(type, query, AccountId, ct);
                return File(file.Content, file.ContentType, file.FileName);
            }
            var result = await _statService.GetRequestStatsAsync(type, query, ct);
            return Ok(result);
        }

        [HttpGet("author-revenue/{metric}")]
        public async Task<IActionResult> GetAuthorRevenue([FromRoute] string metric, [FromQuery] StatQueryRequest query, CancellationToken ct)
        {
             if (query.GenerateReport)
            {
                var file = await _statService.ExportAuthorRevenueStatsAsync(metric, query, AccountId, ct);
                return File(file.Content, file.ContentType, file.FileName);
            }
            var result = await _statService.GetAuthorRevenueStatsAsync(metric, query, ct);
            return Ok(result);
        }

        [HttpGet("traffic/users")]
        public async Task<IActionResult> GetUserGrowth([FromQuery] StatQueryRequest query, CancellationToken ct)
        {
            var result = await _statService.GetUserGrowthStatsAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("traffic/stories/trending")]
        public async Task<IActionResult> GetTrendingStories([FromQuery] StatQueryRequest query, [FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var result = await _statService.GetTrendingStoriesStatsAsync(query, limit, ct);
            return Ok(result);
        }

        [HttpGet("traffic/engagement")]
        public async Task<IActionResult> GetSystemEngagement([FromQuery] StatQueryRequest query, CancellationToken ct)
        {
            var result = await _statService.GetSystemEngagementStatsAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("traffic/tags/top")]
        public async Task<IActionResult> GetTagTrends([FromQuery] StatQueryRequest query, [FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var result = await _statService.GetTagTrendsStatsAsync(query, limit, ct);
            return Ok(result);
        }
    }
}

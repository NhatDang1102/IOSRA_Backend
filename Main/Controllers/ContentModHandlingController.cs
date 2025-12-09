using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Moderation;
using Contract.DTOs.Request.Report;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "cmod,CMOD,CONTENT_MODERATOR,content_moderator")]
    public class ContentModHandlingController : AppControllerBase
    {
        private readonly IContentModHandlingService _service;
        private readonly IReportService _reportService;

        public ContentModHandlingController(
            IContentModHandlingService service,
            IReportService reportService)
        {
            _service = service;
            _reportService = reportService;
        }

        [HttpPut("stories/{storyId:guid}")]
        public async Task<IActionResult> UpdateStoryStatus(Guid storyId, [FromBody] ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var result = await _service.UpdateStoryStatusAsync(AccountId, storyId, request, ct);
            return Ok(result);
        }

        [HttpPut("chapters/{chapterId:guid}")]
        public async Task<IActionResult> UpdateChapterStatus(Guid chapterId, [FromBody] ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var result = await _service.UpdateChapterStatusAsync(AccountId, chapterId, request, ct);
            return Ok(result);
        }

        [HttpPut("comments/{commentId:guid}")]
        public async Task<IActionResult> UpdateCommentStatus(Guid commentId, [FromBody] ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var result = await _service.UpdateCommentStatusAsync(AccountId, commentId, request, ct);
            return Ok(result);
        }

        [HttpPut("accounts/{accountId:guid}/strike-status")]
        public async Task<IActionResult> ApplyStrike(Guid accountId, [FromBody] StrikeLevelUpdateRequest request, CancellationToken ct = default)
        {
            await _service.ApplyStrikeAsync(accountId, request, ct);
            return Ok(new { message = "Strike updated", level = request.Level });
        }

        [HttpGet("reports")]
        public async Task<IActionResult> ListReports([FromQuery] string? status, [FromQuery] string? targetType, [FromQuery] Guid? targetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _reportService.ListAsync(status, targetType, targetId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("reports/{reportId:guid}")]
        public async Task<IActionResult> GetReport(Guid reportId, CancellationToken ct = default)
        {
            var result = await _reportService.GetAsync(reportId, ct);
            return Ok(result);
        }

        [HttpPut("reports/{reportId:guid}/status")]
        public async Task<IActionResult> UpdateReportStatus(Guid reportId, [FromBody] ReportModerationUpdateRequest request, CancellationToken ct = default)
        {
            var result = await _reportService.UpdateStatusAsync(AccountId, reportId, request, ct);
            return Ok(result);
        }
    }
}

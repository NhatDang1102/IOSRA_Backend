using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Admin;
using Contract.DTOs.Response.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Contract.DTOs.Request.Admin;
    using Contract.DTOs.Response.Admin;
    using Contract.DTOs.Response.Common;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Repository.Interfaces;
    using Service.Interfaces;

    namespace Main.Controllers
    {
        [Authorize(Roles = "admin")]
        [Route("api/[controller]")]
        public class AdminController : AppControllerBase
        {
            private readonly IAdminService _adminService;
            private readonly IAuthorChapterRepository _chapterRepository;
            private readonly IChapterContentStorage _contentStorage;
            private readonly IOpenAiModerationService _openAiService;

            public AdminController(
                IAdminService adminService,
                IAuthorChapterRepository chapterRepository,
                IChapterContentStorage contentStorage,
                IOpenAiModerationService openAiService)
            {
                _adminService = adminService;
                _chapterRepository = chapterRepository;
                _contentStorage = contentStorage;
                _openAiService = openAiService;
            }

            [HttpPost("trigger-summary-gen")]
            public IActionResult TriggerSummaryBackfill()
            {
                // Fire-and-forget background task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Process in batches of 10 to be safe
                        while (true)
                        {
                            var chapters = await _chapterRepository.GetChaptersMissingSummaryAsync(10);
                            if (chapters.Count == 0) break;

                            foreach (var chap in chapters)
                            {
                                try
                                {
                                    if (string.IsNullOrWhiteSpace(chap.content_url)) continue;

                                    var content = await _contentStorage.DownloadAsync(chap.content_url);
                                    var summary = await _openAiService.SummarizeChapterAsync(content);

                                    chap.summary = summary;
                                    await _chapterRepository.UpdateAsync(chap);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to summarize chapter {chap.chapter_id}: {ex.Message}");
                                }
                            }

                            // Small delay to be gentle on Rate Limits
                            await Task.Delay(2000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Backfill job failed: {ex.Message}");
                    }
                });

                return Accepted(new { message = "Summary backfill job started in background." });
            }

            [HttpGet("accounts")]
            public async Task<ActionResult<PagedResult<AdminAccountResponse>>> GetAccounts(
                [FromQuery] string? status,
                [FromQuery] string? role,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 20,
                CancellationToken ct = default)
            {
                var result = await _adminService.GetAccountsAsync(status, role, page, pageSize, ct);
                return Ok(result);
            }

            [HttpPost("content-mods")]
            public async Task<ActionResult<AdminAccountResponse>> CreateContentMod([FromBody] CreateModeratorRequest request, CancellationToken ct)
            {
                var result = await _adminService.CreateContentModAsync(request, ct);
                return Ok(result);
            }

            [HttpPost("operation-mods")]
            public async Task<ActionResult<AdminAccountResponse>> CreateOperationMod([FromBody] CreateModeratorRequest request, CancellationToken ct)
            {
                var result = await _adminService.CreateOperationModAsync(request, ct);
                return Ok(result);
            }

            [HttpPatch("accounts/{accountId:guid}/status")]
            public async Task<ActionResult<AdminAccountResponse>> UpdateStatus(Guid accountId, [FromBody] UpdateAccountStatusRequest request, CancellationToken ct)
            {
                var result = await _adminService.UpdateStatusAsync(accountId, request, ct);
                return Ok(result);
            }
        }
    }
}

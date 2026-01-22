using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize]
    [Route("api/AuthorRevenue")]
    public class AuthorRevenueController : AppControllerBase
    {
        private readonly IAuthorRevenueService _authorRevenueService;

        public AuthorRevenueController(IAuthorRevenueService authorRevenueService)
        {
            _authorRevenueService = authorRevenueService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<AuthorRevenueSummaryResponse>> GetSummary(CancellationToken ct)
        {
            var result = await _authorRevenueService.GetSummaryAsync(AccountId, ct);
            return Ok(result);
        }

        [HttpGet("transactions")]
        public async Task<ActionResult<PagedResult<AuthorRevenueTransactionItemResponse>>> GetTransactions([FromQuery] AuthorRevenueTransactionQuery query, CancellationToken ct)
        {
            var result = await _authorRevenueService.GetTransactionsAsync(AccountId, query, ct);
            return Ok(result);
        }

        [HttpPost("withdraw")]
        public async Task<ActionResult<AuthorWithdrawRequestResponse>> SubmitWithdraw([FromBody] AuthorWithdrawRequest request, CancellationToken ct)
        {
            var result = await _authorRevenueService.SubmitWithdrawAsync(AccountId, request, ct);
            return Ok(result);
        }

        [HttpPost("withdraw/{requestId:guid}/confirm")]
        public async Task<IActionResult> ConfirmReceipt(Guid requestId, CancellationToken ct)
        {
            await _authorRevenueService.ConfirmReceiptAsync(AccountId, requestId, ct);
            return Ok(new { message = "Receipt confirmed." });
        }

        [HttpGet("withdraw")]
        public async Task<ActionResult<IReadOnlyList<AuthorWithdrawRequestResponse>>> ListWithdrawRequests([FromQuery] string? status, CancellationToken ct)
        {
            var result = await _authorRevenueService.GetWithdrawRequestsAsync(AccountId, status, ct);
            return Ok(result);
        }

        [HttpGet("stories/{storyId}")]
        public async Task<ActionResult<StoryRevenueDetailResponse>> GetStoryRevenue(Guid storyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _authorRevenueService.GetStoryRevenueDetailAsync(AccountId, storyId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("chapters/{chapterId}")]
        public async Task<ActionResult<ContentRevenueDetailResponse>> GetChapterRevenue(Guid chapterId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _authorRevenueService.GetChapterRevenueDetailAsync(AccountId, chapterId, page, pageSize, ct);
            return Ok(result);
        }
    }
}

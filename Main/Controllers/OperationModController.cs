using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Contract.DTOs.Request.OperationMod;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;
using Contract.DTOs.Response.Author;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "omod,OPERATION_MOD")]
    public class OperationModController : AppControllerBase
    {
        private readonly IOperationModService _service;
        private readonly IAuthorRankPromotionService _rankPromotionService;

        public OperationModController(IOperationModService service, IAuthorRankPromotionService rankPromotionService)
        {
            _service = service;
            _rankPromotionService = rankPromotionService;
        }

        [HttpGet("requests")]
        public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        {
            var result = await _service.ListAsync(status, ct);
            return Ok(result);
        }

        [HttpPost("requests/{requestId:guid}/approve")]
        public async Task<IActionResult> Approve([FromRoute] Guid requestId, CancellationToken ct)
        {
            await _service.ApproveAsync(requestId, AccountId, ct);
            return Ok(new { message = "Approved" });
        }

        [HttpPost("requests/{requestId:guid}/reject")]
        public async Task<IActionResult> Reject([FromRoute] Guid requestId, [FromBody] RejectAuthorUpgradeRequest request, CancellationToken ct)
        {
            await _service.RejectAsync(requestId, AccountId, request, ct);
            return Ok(new { message = "Rejected" });
        }

        [HttpGet("rank-requests")]
        public async Task<IActionResult> ListRankRequests([FromQuery] string? status, CancellationToken ct)
        {
            var result = await _rankPromotionService.ListForModerationAsync(status, ct);
            return Ok(result);
        }

        [HttpPost("rank-requests/{requestId:guid}/approve")]
        public async Task<IActionResult> ApproveRankRequest([FromRoute] Guid requestId, [FromBody] RankPromotionApproveRequest? request, CancellationToken ct)
        {
            await _rankPromotionService.ApproveAsync(requestId, AccountId, request?.Note, ct);
            return Ok(new { message = "Approved" });
        }

        [HttpPost("rank-requests/{requestId:guid}/reject")]
        public async Task<IActionResult> RejectRankRequest([FromRoute] Guid requestId, [FromBody] RankPromotionRejectRequest request, CancellationToken ct)
        {
            await _rankPromotionService.RejectAsync(requestId, AccountId, request, ct);
            return Ok(new { message = "Rejected" });
        }

        [HttpGet("withdraw-requests")]
        public async Task<ActionResult<IReadOnlyList<AuthorWithdrawRequestResponse>>> ListWithdrawRequests([FromQuery] string? status, CancellationToken ct)
        {
            var result = await _service.ListWithdrawRequestsAsync(status, ct);
            return Ok(result);
        }

        [HttpPost("withdraw-requests/{requestId:guid}/approve")]
        public async Task<IActionResult> ApproveWithdraw(Guid requestId, [FromBody] ApproveWithdrawRequest? request, CancellationToken ct)
        {
            await _service.ApproveWithdrawAsync(requestId, AccountId, request ?? new ApproveWithdrawRequest(), ct);
            return Ok(new { message = "Approved" });
        }

        [HttpPost("withdraw-requests/{requestId:guid}/reject")]
        public async Task<IActionResult> RejectWithdraw(Guid requestId, [FromBody] RejectWithdrawRequest request, CancellationToken ct)
        {
            await _service.RejectWithdrawAsync(requestId, AccountId, request, ct);
            return Ok(new { message = "Rejected" });
        }

    }
}

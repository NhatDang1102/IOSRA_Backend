using Contract.DTOs.Request.OperationMod;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [Authorize(Roles = "omod,OPERATION_MOD,admin,ADMIN")]
    public class OperationModController : AppControllerBase
    {
        private readonly IOperationModService _svc;

        public OperationModController(IOperationModService svc)
        {
            _svc = svc;
        }

        // GET /api/operationmod/requests?status=pending|approved|rejected|null
        [HttpGet("requests")]
        public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
        {
            var res = await _svc.ListAsync(status, ct);
            return Ok(res);
        }

        // POST /api/operationmod/requests/{id}/approve
        [HttpPost("requests/{requestId}/approve")]
        public async Task<IActionResult> Approve([FromRoute] ulong requestId, CancellationToken ct)
        {
            await _svc.ApproveAsync(requestId, AccountId, ct);
            return Ok(new { message = "Approved" });
        }

        // POST /api/operationmod/requests/{id}/reject
        [HttpPost("requests/{requestId}/reject")]
        public async Task<IActionResult> Reject([FromRoute] ulong requestId, [FromBody] RejectAuthorUpgradeRequest req, CancellationToken ct)
        {
            await _svc.RejectAsync(requestId, AccountId, req, ct);
            return Ok(new { message = "Rejected" });
        }
    }
}

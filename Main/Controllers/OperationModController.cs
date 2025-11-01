using System;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IOperationModService _service;

        public OperationModController(IOperationModService service)
        {
            _service = service;
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
    }
}

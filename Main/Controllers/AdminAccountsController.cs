using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Respond.Admin;
using Contract.DTOs.Respond.Common;
using Main.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    /// <summary>
    /// Admin account management endpoints (base route: /admin/accounts).
    /// Requires the AdminOnly policy.
    /// </summary>
    [ApiController]
    [Route("admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    public class AccountsController : ControllerBase
    {
        private readonly IAdminService _service;

        public AccountsController(IAdminService service)
        {
            _service = service;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AccountAdminResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get([FromQuery] AccountQuery query, CancellationToken ct)
        {
            var result = await _service.QueryAccountsAsync(query, ct);
            return Ok(result);
        }

        [HttpGet("find")]
        [ProducesResponseType(typeof(AccountAdminResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Find([FromQuery] string identifier, CancellationToken ct)
        {
            var item = await _service.GetAccountByIdentifierAsync(identifier, ct);
            return Ok(item);
        }

        [HttpPost("roles/{accountId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetRoles([FromRoute] Guid accountId, [FromBody] UpdateRolesRequest request, CancellationToken ct)
        {
            await _service.SetRolesAsync(accountId, request.RoleCodes, ct);
            return NoContent();
        }

        [HttpPost("ban/{accountId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Ban([FromRoute] Guid accountId, [FromBody] BanUnbanRequest request, CancellationToken ct)
        {
            await _service.BanAsync(accountId, request.Reason, ct);
            return NoContent();
        }

        [HttpPost("unban/{accountId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unban([FromRoute] Guid accountId, [FromBody] BanUnbanRequest request, CancellationToken ct)
        {
            await _service.UnbanAsync(accountId, request.Reason, ct);
            return NoContent();
        }
    }
}

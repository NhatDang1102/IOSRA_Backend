using Contract.DTOs.Request.Admin;
using Contract.DTOs.Respond.Admin;
using Main.Models; // ErrorResponse
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    /// <summary>
    /// API quản trị tài khoản (route gốc: /admin/accounts)
    /// Yêu cầu: role ADMIN (Policy: AdminOnly)
    /// </summary>
    [ApiController]
    [Route("admin/[controller]")]
    [Authorize(Policy = "AdminOnly")]
    public class AccountsController : ControllerBase
    {
        private readonly IAdminService _svc;
        public AccountsController(IAdminService svc) => _svc = svc;

        /// <summary>Danh sách tài khoản (lọc & phân trang)</summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<AccountAdminResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Get([FromQuery] AccountQuery q, CancellationToken ct)
        {
            var res = await _svc.QueryAccountsAsync(q, ct);
            return Ok(res);
        }

        /// <summary>Tìm 1 tài khoản theo email hoặc username</summary>
        [HttpGet("find")]
        [ProducesResponseType(typeof(AccountAdminResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Find([FromQuery] string identifier, CancellationToken ct)
        {
            var item = await _svc.GetAccountByIdentifierAsync(identifier, ct);
            return Ok(item);
        }

        /// <summary>Gán/cập nhật role theo role_code</summary>
        /// <remarks>Body: { "roleCodes": ["ADMIN","READER"] }</remarks>
        [HttpPost("roles/{accountId:long}")] // ✅ gắn dưới /admin/accounts
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetRoles([FromRoute] ulong accountId, [FromBody] UpdateRolesRequest req, CancellationToken ct)
        {
            await _svc.SetRolesAsync(accountId, req.RoleCodes, ct);
            return NoContent();
        }

        /// <summary>Ban tài khoản</summary>
        /// <remarks>Body: { "reason": "vi phạm ..." }</remarks>
        [HttpPost("ban/{accountId:long}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Ban([FromRoute] ulong accountId, [FromBody] BanUnbanRequest req, CancellationToken ct)
        {
            await _svc.BanAsync(accountId, req.Reason, ct);
            return NoContent();
        }

        /// <summary>Unban tài khoản</summary>
        /// <remarks>Body: { "reason": "đã xem xét ..." } (tuỳ dùng hay không)</remarks>
        [HttpPost("unban/{accountId:long}")] // ✅
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unban([FromRoute] ulong accountId, [FromBody] BanUnbanRequest req, CancellationToken ct)
        {
            await _svc.UnbanAsync(accountId, req.Reason, ct);
            return NoContent();
        }
    }
}

using System.ComponentModel.DataAnnotations;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Respond.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers;

/// <summary>
/// API quản trị (route gốc: /api/admin)
/// Yêu cầu: role ADMIN (Policy: AdminOnly)
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "AdminOnly")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService) => _adminService = adminService;

    /// <summary>Danh sách tài khoản (lọc & phân trang)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AccountAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts([FromQuery] AccountQuery query, CancellationToken ct)
        => Ok(await _adminService.QueryAccountsAsync(query, ct));

    /// <summary>Tìm 1 tài khoản theo email hoặc username</summary>
    [HttpGet("find")]
    [ProducesResponseType(typeof(AccountAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FindAccount(
        [FromQuery, Required, MaxLength(255)] string identifier,
        CancellationToken ct)
        => Ok(await _adminService.GetAccountByIdentifierAsync(identifier, ct));

    /// <summary>Gán/cập nhật role theo role_code</summary>
    /// <remarks>Body: { "roleCodes": ["ADMIN","READER"] }</remarks>
    [HttpPost("roles/{accountId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetRoles(
        ulong accountId,
        [FromBody] UpdateRolesRequest req,
        CancellationToken ct)
    {
        await _adminService.SetRolesAsync(accountId, req.RoleCodes, ct);
        return NoContent();
    }

    /// <summary>Ban tài khoản</summary>
    /// <remarks>Body: { "reason": "vi phạm ..." }</remarks>
    [HttpPost("ban/{accountId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Ban(
        ulong accountId,
        [FromBody] BanUnbanRequest req,
        CancellationToken ct)
    {
        await _adminService.BanAsync(accountId, req.Reason, ct);
        return NoContent();
    }

    /// <summary>Unban tài khoản</summary>
    /// <remarks>Body: { "reason": "đã xem xét ..." } (tuỳ dùng)</remarks>
    [HttpPost("unban/{accountId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unban(
        ulong accountId,
        [FromBody] BanUnbanRequest req,
        CancellationToken ct)
    {
        await _adminService.UnbanAsync(accountId, req.Reason, ct);
        return NoContent();
    }
}

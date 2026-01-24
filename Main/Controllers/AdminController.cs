using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Admin;
using Contract.DTOs.Response.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Interfaces;

namespace Main.Controllers;

[Authorize(Roles = "admin")]
[Route("api/[controller]")]
public class AdminController : AppControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ISystemHealthService _systemHealthService;

    public AdminController(IAdminService adminService, ISystemHealthService systemHealthService)
    {
        _adminService = adminService;
        _systemHealthService = systemHealthService;
    }

    [HttpGet("accounts")]
    public async Task<ActionResult<PagedResult<AdminAccountResponse>>> GetAccounts(
        [FromQuery] string? status,
        [FromQuery] string? role,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _adminService.GetAccountsAsync(status, role, search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("content-mods")]
    public async Task<ActionResult<AdminAccountResponse>> CreateContentMod(
        [FromBody] CreateModeratorRequest request,
        CancellationToken ct)
    {
        var result = await _adminService.CreateContentModAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("operation-mods")]
    public async Task<ActionResult<AdminAccountResponse>> CreateOperationMod(
        [FromBody] CreateModeratorRequest request,
        CancellationToken ct)
    {
        var result = await _adminService.CreateOperationModAsync(request, ct);
        return Ok(result);
    }

    [HttpPatch("accounts/{accountId:guid}/status")]
    public async Task<ActionResult<AdminAccountResponse>> UpdateStatus(
        Guid accountId,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken ct)
    {
        var result = await _adminService.UpdateStatusAsync(accountId, request, ct);
        return Ok(result);
    }

    // UPTIME (Admin IT)
    // - Dùng cho dashboard vận hành: hiển thị thời điểm instance start và thời gian chạy liên tục
    // - Lưu ý: đây là uptime của process/instance hiện tại (không đại diện cho toàn hệ thống nếu scale-out)
    [HttpGet("uptime")]
    public ActionResult<UptimeResponse> GetUptime()
    {
        return Ok(new UptimeResponse
        {
            StartedAtUtc = SystemUptimeSnapshot.StartedAtUtc,
            UptimeSeconds = SystemUptimeSnapshot.UptimeSeconds
        });
    }

    // HEALTH MONITOR (Admin IT)
    // - Trả về status tổng quan + map các thành phần để FE render 
    [Authorize(Roles = "admin,omod,OPERATION_MOD")]
    [HttpGet("health")]
    public async Task<ActionResult<HealthResponse>> Health(CancellationToken ct)
    {
        var result = await _systemHealthService.CheckAsync(ct);
        return Ok(result);
    }
}

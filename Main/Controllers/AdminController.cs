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
    using Service.Helpers;
    using Service.Interfaces;
    
    namespace Main.Controllers
    {
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

            // Public nhẹ (chỉ để check uptime API có sống không)
            [HttpGet("uptime")]
            public IActionResult GetUptime()
            {
                return Ok(SystemUptimeSnapshot.Current);
            }

            // Chi tiết health 
            [Authorize(Roles = "admin,omod,OPERATION_MOD")]
            [HttpGet("health")]
            public async Task<IActionResult> Health(CancellationToken ct)
            {
                var result = await _systemHealthService.CheckAsync(ct);
                return Ok(result);
            }
        }
    }
}

using Contract.DTOs.Response.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers;

[Authorize(Roles = "admin")]
[Route("api/[controller]")]
public class AdminBackupController : AppControllerBase
{
    private readonly IBackupService _backupService;

    public AdminBackupController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet("capabilities")]
    public async Task<ActionResult<BackupCapabilitiesResponse>> Capabilities(CancellationToken ct)
            => Ok(await _backupService.GetCapabilitiesAsync(ct));

    [HttpPost("run")]
    public async Task<ActionResult<BackupRunResponse>> Run(CancellationToken ct)
        => Ok(await _backupService.RunAsync(ct));

    [HttpGet("history")]
    public async Task<ActionResult<List<BackupHistoryItemResponse>>> History(CancellationToken ct)
        => Ok(await _backupService.GetHistoryAsync(ct));

    [HttpPost("restore/{backupId}")]
    public async Task<ActionResult<BackupRunResponse>> Restore(string backupId, CancellationToken ct)
        => Ok(await _backupService.RestoreAsync(backupId, ct));
}

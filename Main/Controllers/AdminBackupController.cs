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

    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        var result = await _backupService.RunAsync(ct);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var result = await _backupService.GetHistoryAsync(ct);
        return Ok(result);
    }

    [HttpPost("restore/{backupId}")]
    public async Task<IActionResult> Restore(string backupId, CancellationToken ct)
    {
        var result = await _backupService.RestoreAsync(backupId, ct);
        return Ok(result);
    }
}

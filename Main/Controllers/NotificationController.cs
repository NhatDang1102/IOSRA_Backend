using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : AppControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _notificationService.GetAsync(AccountId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("{notificationId:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct = default)
        {
            await _notificationService.MarkReadAsync(AccountId, notificationId, ct);
            return NoContent();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
        {
            await _notificationService.MarkAllReadAsync(AccountId, ct);
            return NoContent();
        }
    }
}

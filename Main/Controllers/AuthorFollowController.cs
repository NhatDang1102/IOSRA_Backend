using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Follow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Route("api/[controller]")]
    public class AuthorFollowController : AppControllerBase
    {
        private readonly IAuthorFollowService _followService;

        public AuthorFollowController(IAuthorFollowService followService)
        {
            _followService = followService;
        }

        [HttpPost("{authorId:guid}")]
        [Authorize]
        public async Task<IActionResult> Follow(Guid authorId, [FromBody] AuthorFollowRequest? request, CancellationToken ct)
        {
            var payload = request ?? new AuthorFollowRequest();
            var result = await _followService.FollowAsync(AccountId, authorId, payload, ct);
            return Ok(result);
        }

        [HttpDelete("{authorId:guid}")]
        [Authorize]
        public async Task<IActionResult> Unfollow(Guid authorId, CancellationToken ct)
        {
            await _followService.UnfollowAsync(AccountId, authorId, ct);
            return NoContent();
        }

        [HttpPatch("{authorId:guid}/notification")]
        [Authorize]
        public async Task<IActionResult> UpdateNotification(Guid authorId, [FromBody] AuthorFollowNotificationRequest request, CancellationToken ct)
        {
            var result = await _followService.UpdateNotificationAsync(AccountId, authorId, request.EnableNotifications, ct);
            return Ok(result);
        }

        [HttpGet("{authorId:guid}/followers")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFollowers(Guid authorId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _followService.GetFollowersAsync(authorId, page, pageSize, ct);
            return Ok(result);
        }
    }
}

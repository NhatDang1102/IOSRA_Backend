using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Story;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;

namespace Main.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    public class FavoriteStoryController : AppControllerBase
    {
        private readonly IFavoriteStoryService _favoriteService;

        public FavoriteStoryController(IFavoriteStoryService favoriteService)
        {
            _favoriteService = favoriteService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<FavoriteStoryResponse>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var result = await _favoriteService.ListAsync(AccountId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpPost("{storyId:guid}")]
        public async Task<ActionResult<FavoriteStoryResponse>> Add(Guid storyId, CancellationToken ct)
        {
            var result = await _favoriteService.AddAsync(AccountId, storyId, ct);
            return Ok(result);
        }

        [HttpDelete("{storyId:guid}")]
        public async Task<IActionResult> Remove(Guid storyId, CancellationToken ct)
        {
            await _favoriteService.RemoveAsync(AccountId, storyId, ct);
            return NoContent();
        }

        [HttpPut("{storyId:guid}/notifications")]
        public async Task<ActionResult<FavoriteStoryResponse>> Toggle(Guid storyId, FavoriteStoryNotificationRequest request, CancellationToken ct)
        {
            var result = await _favoriteService.ToggleNotificationAsync(AccountId, storyId, request, ct);
            return Ok(result);
        }
    }
}

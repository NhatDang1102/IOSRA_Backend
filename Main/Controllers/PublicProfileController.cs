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
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class PublicProfileController : AppControllerBase
    {
        private readonly IPublicProfileService _publicProfileService;
        private readonly IStoryCatalogService _storyCatalogService;

        public PublicProfileController(IPublicProfileService publicProfileService, IStoryCatalogService storyCatalogService)
        {
            _publicProfileService = publicProfileService;
            _storyCatalogService = storyCatalogService;
        }

        [HttpGet("{accountId:guid}")]
        public async Task<IActionResult> GetProfile(Guid accountId, CancellationToken ct)
        {
            var profile = await _publicProfileService.GetAsync(TryGetAccountId() ?? Guid.Empty, accountId, ct);
            return Ok(profile);
        }

        [HttpGet("{accountId:guid}/stories")]
        public async Task<ActionResult<PagedResult<StoryCatalogListItemResponse>>> GetAuthorStories(Guid accountId, [FromQuery] StoryCatalogQuery query, CancellationToken ct)
        {
            var profile = await _publicProfileService.GetAsync(TryGetAccountId() ?? Guid.Empty, accountId, ct);

            if (!profile.IsAuthor || profile.Author is null || profile.Author.IsRestricted)
            {
                return Ok(new PagedResult<StoryCatalogListItemResponse>
                {
                    Items = Array.Empty<StoryCatalogListItemResponse>(),
                    Total = 0,
                    Page = query.Page,
                    PageSize = query.PageSize
                });
            }

            query.AuthorId = accountId;
            var stories = await _storyCatalogService.GetStoriesAsync(query, ct);
            return Ok(stories);
        }
    }
}

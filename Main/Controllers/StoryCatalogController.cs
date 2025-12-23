using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Story;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Main.Controllers
{
    // Controller public để xem danh sách truyện (không cần đăng nhập)
    [Route("api/StoryCatalog")]
    [AllowAnonymous]
    public class StoryCatalogController : AppControllerBase
    {
        private readonly IStoryCatalogService _storyCatalogService;
        private readonly IStoryHighlightService _storyHighlightService;

        public StoryCatalogController(IStoryCatalogService storyCatalogService, IStoryHighlightService storyHighlightService)
        {
            _storyCatalogService = storyCatalogService;
            _storyHighlightService = storyHighlightService;
        }

        // API lấy danh sách truyện cơ bản (Legacy)
        [HttpGet]
        public async Task<ActionResult<PagedResult<StoryCatalogListItemResponse>>> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? query = null,
            [FromQuery] Guid? tagId = null,
            [FromQuery] Guid? authorId = null,
            [FromQuery] string? languageCode = null,
            CancellationToken ct = default)
        {
            var queryObj = new StoryCatalogQuery
            {
                Page = page,
                PageSize = pageSize,
                Query = query,
                TagId = tagId,
                AuthorId = authorId,
                LanguageCode = languageCode
            };

            var result = await _storyCatalogService.GetStoriesAsync(queryObj, ct);
            return Ok(result);
        }

        // API Tìm kiếm & Lọc nâng cao (Advanced Filter)
        // Hỗ trợ lọc theo Premium, Rating, Sort (Newest, Popular, Trending)
        [HttpGet("advance-filter")]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<StoryCatalogListItemResponse>>> Filter(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery(Name = "Query")] string? queryText = null,
            [FromQuery] Guid? tagId = null,
            [FromQuery] Guid? authorId = null,
            [FromQuery] string? languageCode = null,
            [FromQuery] bool? isPremium = null,
            [FromQuery] double? minAvgRating = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDir = null,
            CancellationToken ct = default)
        {
            var query = new StoryCatalogQuery
            {
                Page = page,
                PageSize = pageSize,
                Query = queryText,
                TagId = tagId,
                AuthorId = authorId,
                LanguageCode = languageCode,
                IsPremium = isPremium,
                MinAvgRating = minAvgRating,
            };

            if (!string.IsNullOrWhiteSpace(sortBy) &&
                Enum.TryParse<StorySortBy>(sortBy, ignoreCase: true, out var parsedSortBy))
            {
                query.SortBy = parsedSortBy;
            }

            if (!string.IsNullOrWhiteSpace(sortDir) &&
                Enum.TryParse<SortDir>(sortDir, ignoreCase: true, out var parsedSortDir))
            {
                query.SortDir = parsedSortDir;
            }

            var result = await _storyCatalogService.GetStoriesAdvancedAsync(query, ct);
            return Ok(result);
        }

        // API lấy danh sách truyện mới nhất (được cache trong Redis để tối ưu tốc độ load trang chủ)
        [HttpGet("latest")]
        public async Task<ActionResult<IReadOnlyList<StoryCatalogListItemResponse>>> Latest([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var items = await _storyHighlightService.GetLatestStoriesAsync(limit, ct);
            return Ok(items);
        }

        // API lấy top truyện thịnh hành trong tuần (dựa trên view count)
        [HttpGet("top-weekly")]
        public async Task<ActionResult<IReadOnlyList<StoryWeeklyHighlightResponse>>> TopWeekly([FromQuery] int limit = 10, CancellationToken ct = default)
        {
            var items = await _storyHighlightService.GetTopWeeklyStoriesAsync(limit, ct);
            return Ok(items);
        }

        // API lấy chi tiết một truyện theo ID (Trang Detail)
        [HttpGet("{storyId:guid}")]
        public async Task<ActionResult<StoryCatalogDetailResponse>> Get(Guid storyId, CancellationToken ct)
        {
            var story = await _storyCatalogService.GetStoryAsync(storyId, ct);
            return Ok(story);
        }
    }
}
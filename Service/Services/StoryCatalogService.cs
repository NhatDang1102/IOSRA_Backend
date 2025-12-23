using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Story;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;

namespace Service.Services
{
    // Service xử lý việc hiển thị danh sách truyện cho độc giả (Catalog)
    public class StoryCatalogService : IStoryCatalogService
    {
        private readonly IStoryCatalogRepository _storyRepository;
        private readonly IChapterCatalogRepository _chapterRepository;

        public StoryCatalogService(IStoryCatalogRepository storyRepository, IChapterCatalogRepository chapterRepository)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
        }

        // Tìm kiếm nâng cao với nhiều bộ lọc (Tag, Author, Rating, Premium...)
        public async Task<PagedResult<StoryCatalogListItemResponse>> GetStoriesAdvancedAsync(StoryCatalogQuery query, CancellationToken ct = default)
        {
            // validate page info
            if (query.Page < 1 || query.PageSize < 1)
                throw new AppException("ValidationFailed", "Page and PageSize must be positive integers.", 400);

            // Tính toán thời điểm bắt đầu tuần hiện tại (UTC) để phục vụ việc sort theo Lượt xem tuần (Weekly Views)
            var weekStartUtc = StoryViewTimeHelper.GetCurrentWeekStartUtc();

            // Gọi Repository thực hiện query phức tạp (Join nhiều bảng)
            var (stories, total) = await _storyRepository.SearchPublishedStoriesAdvancedAsync(
                query.Query,
                query.TagId,
                query.AuthorId,
                query.LanguageCode,
                query.IsPremium,
                query.MinAvgRating,
                query.SortBy.ToString(),
                query.SortDir == SortDir.Desc,
                weekStartUtc,
                query.Page,
                query.PageSize,
                ct
            );

            if (stories.Count == 0)
            {
                return new PagedResult<StoryCatalogListItemResponse>
                {
                    Items = Array.Empty<StoryCatalogListItemResponse>(),
                    Total = total,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }

            // Lấy thêm thông tin tổng số chương đã xuất bản cho từng truyện trong danh sách
            // (Thực hiện query riêng để tối ưu hiệu năng thay vì Count() trong từng dòng SQL chính)
            var storyIds = stories.Select(s => s.story_id).ToArray();
            var chapterCounts = await _chapterRepository.GetPublishedChapterCountsByStoryIdsAsync(storyIds, ct);

            // Map dữ liệu Entity -> DTO trả về cho Client
            var items = stories
                .Select(s => StoryCatalogMapper.ToListItemResponse(s, chapterCounts))
                .ToList();

            return new PagedResult<StoryCatalogListItemResponse>
            {
                Items = items,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        // Tìm kiếm cơ bản (Legacy, ít bộ lọc hơn)
        public async Task<PagedResult<StoryCatalogListItemResponse>> GetStoriesAsync(StoryCatalogQuery query, CancellationToken ct = default)
        {
            if (query.Page < 1 || query.PageSize < 1)
            {
                throw new AppException("ValidationFailed", "Page and PageSize must be positive integers.", 400);
            }

            var (stories, total) = await _storyRepository.SearchPublishedStoriesAsync(query.Query, query.TagId, query.AuthorId, query.LanguageCode, query.Page, query.PageSize, ct);

            if (stories.Count == 0)
            {
                return new PagedResult<StoryCatalogListItemResponse>
                {
                    Items = Array.Empty<StoryCatalogListItemResponse>(),
                    Total = total,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }

            var storyIds = stories.Select(s => s.story_id).ToArray();
            var chapterCounts = await _chapterRepository.GetPublishedChapterCountsByStoryIdsAsync(storyIds, ct);

            var responses = stories
                .Select(story => StoryCatalogMapper.ToListItemResponse(story, chapterCounts))
                .ToList();

            return new PagedResult<StoryCatalogListItemResponse>
            {
                Items = responses,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        // Lấy chi tiết một bộ truyện (Trang giới thiệu truyện)
        public async Task<StoryCatalogDetailResponse> GetStoryAsync(Guid storyId, CancellationToken ct = default)
        {
            // Chỉ lấy truyện đã Publish (hoặc Completed)
            var story = await _storyRepository.GetPublishedStoryByIdAsync(storyId, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found or not available.", 404);

            var totalChapters = await _chapterRepository.GetPublishedChapterCountAsync(story.story_id, ct);

            // Sắp xếp Tags theo tên để hiển thị đẹp hơn
            var tags = story.story_tags?
                .Where(st => st.tag != null)
                .Select(st => new StoryTagResponse { TagId = st.tag_id, TagName = st.tag!.tag_name })
                .OrderBy(t => t.TagName, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<StoryTagResponse>();

            return new StoryCatalogDetailResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                LanguageCode = story.language?.lang_code ?? string.Empty,
                LanguageName = story.language?.lang_name ?? string.Empty,
                AuthorId = story.author_id,
                AuthorUsername = story.author.account.username,
                CoverUrl = story.cover_url,
                IsPremium = story.is_premium,
                Status = story.status,
                Description = story.desc,
                TotalChapters = totalChapters,
                TotalViews = story.total_views,
                PublishedAt = story.published_at,
                LengthPlan = story.length_plan,
                Tags = tags
            };
        }

    }
}
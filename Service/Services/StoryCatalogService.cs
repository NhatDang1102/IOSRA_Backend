using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Common;
using Contract.DTOs.Respond.Story;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;

namespace Service.Services
{
    public class StoryCatalogService : IStoryCatalogService
    {
        private readonly IStoryCatalogRepository _storyRepository;
        private readonly IChapterCatalogRepository _chapterRepository;

        public StoryCatalogService(IStoryCatalogRepository storyRepository, IChapterCatalogRepository chapterRepository)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
        }

        public async Task<PagedResult<StoryCatalogListItemResponse>> GetStoriesAdvancedAsync(StoryCatalogQuery query, CancellationToken ct = default)
        {
            // validate page info
            if (query.Page < 1 || query.PageSize < 1)
                throw new AppException("ValidationFailed", "Page and PageSize must be positive integers.", 400);

            // calculate start of week for sorting WeeklyViews
            var weekStartUtc = StoryViewTimeHelper.GetCurrentWeekStartUtc();

            var (stories, total) = await _storyRepository.SearchPublishedStoriesAdvancedAsync(
                query.Query,
                query.TagId,
                query.AuthorId,
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

            var storyIds = stories.Select(s => s.story_id).ToArray();
            var chapterCounts = await _chapterRepository.GetPublishedChapterCountsByStoryIdsAsync(storyIds, ct);

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

        public async Task<PagedResult<StoryCatalogListItemResponse>> GetStoriesAsync(StoryCatalogQuery query, CancellationToken ct = default)
        {
            if (query.Page < 1 || query.PageSize < 1)
            {
                throw new AppException("ValidationFailed", "Page and PageSize must be positive integers.", 400);
            }

            var (stories, total) = await _storyRepository.SearchPublishedStoriesAsync(query.Query, query.TagId, query.AuthorId, query.Page, query.PageSize, ct);

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

        public async Task<StoryCatalogDetailResponse> GetStoryAsync(Guid storyId, CancellationToken ct = default)
        {
            var story = await _storyRepository.GetPublishedStoryByIdAsync(storyId, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found or not available.", 404);

            var totalChapters = await _chapterRepository.GetPublishedChapterCountAsync(story.story_id, ct);

            var tags = story.story_tags?
                .Where(st => st.tag != null)
                .Select(st => new StoryTagResponse { TagId = st.tag_id, TagName = st.tag!.tag_name })
                .OrderBy(t => t.TagName, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<StoryTagResponse>();

            return new StoryCatalogDetailResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                AuthorId = story.author_id,
                AuthorUsername = story.author.account.username,
                CoverUrl = story.cover_url,
                IsPremium = story.is_premium,
                Description = story.desc,
                TotalChapters = totalChapters,
                PublishedAt = story.published_at,
                LengthPlan = story.length_plan,
                Tags = tags
            };
        }

    }
}

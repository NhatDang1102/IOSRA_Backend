using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Contract.DTOs.Response.Story;
using Repository.Interfaces;
using Service.Helpers;
using Service.Interfaces;

namespace Service.Services
{
    public class StoryHighlightService : IStoryHighlightService
    {
        //bóc top 10
        private const int DefaultLimit = 10;

        private readonly IStoryCatalogRepository _storyRepository;
        private readonly IChapterCatalogRepository _chapterRepository;
        private readonly IStoryViewTracker _storyViewTracker;
        private readonly IStoryWeeklyViewRepository _storyWeeklyViewRepository;

        public StoryHighlightService(
            IStoryCatalogRepository storyRepository,
            IChapterCatalogRepository chapterRepository,
            IStoryViewTracker storyViewTracker,
            IStoryWeeklyViewRepository storyWeeklyViewRepository)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _storyViewTracker = storyViewTracker;
            _storyWeeklyViewRepository = storyWeeklyViewRepository;
        }

        public async Task<IReadOnlyList<StoryCatalogListItemResponse>> GetLatestStoriesAsync(int limit, CancellationToken ct = default)
        {
            //lấy (limit)  truyện mới phát hành 
            var latestStories = await _storyRepository.GetLatestPublishedStoriesAsync(limit <= 0 ? DefaultLimit : limit, ct);

            if (latestStories.Count == 0)
            {
                return Array.Empty<StoryCatalogListItemResponse>();
            }

            var chapterCounts = await _chapterRepository.GetPublishedChapterCountsByStoryIdsAsync(latestStories.Select(s => s.story_id), ct);
            return latestStories
                .Select(story => StoryCatalogMapper.ToListItemResponse(story, chapterCounts))
                .ToList();
        }

        public async Task<IReadOnlyList<StoryWeeklyHighlightResponse>> GetTopWeeklyStoriesAsync(int limit, CancellationToken ct = default)
        {
            //đồng bộ hóa thời gian
            var weekStart = _storyViewTracker.GetCurrentWeekStartUtc();
            //bóc top limit danh sách story trong redis ra, nếu fail thì bóc trong db
            var top = await _storyViewTracker.GetWeeklyTopAsync(weekStart, limit <= 0 ? DefaultLimit : limit, ct);

            if (top.Count == 0)
            {
                top = await _storyWeeklyViewRepository.GetTopWeeklyViewsAsync(weekStart, limit <= 0 ? DefaultLimit : limit, ct);
            }

            if (top.Count == 0)
            {
                return Array.Empty<StoryWeeklyHighlightResponse>();
            }

            var storyIds = top.Select(x => x.StoryId).ToArray();
            var stories = await _storyRepository.GetStoriesByIdsAsync(storyIds, ct);

            //chuyển hết metadata lấy ở dòng trên thành dictionary với key là story_id
            var storyMap = stories.ToDictionary(s => s.story_id, s => s);
            var chapterCounts = await _chapterRepository.GetPublishedChapterCountsByStoryIdsAsync(storyIds, ct);
            //foreach đảm bảo result cuối dựa theo top rank ko phải thứ tự
            var ordered = new List<StoryWeeklyHighlightResponse>();
            foreach (var item in top)
            {
                if (!storyMap.TryGetValue(item.StoryId, out var story))
                {
                    continue;
                }
                //map hết metadata và data thống kê vào dto response
                ordered.Add(new StoryWeeklyHighlightResponse
                {
                    Story = StoryCatalogMapper.ToListItemResponse(story, chapterCounts),
                    WeeklyViewCount = item.ViewCount,
                    WeekStartUtc = weekStart
                });
            }

            return ordered;
        }
    }
}

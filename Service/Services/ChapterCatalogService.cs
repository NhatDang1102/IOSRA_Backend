using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Contract.DTOs.Respond.Common;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ChapterCatalogService : IChapterCatalogService
    {
        private readonly IChapterCatalogRepository _chapterRepository;
        private readonly IStoryCatalogRepository _storyRepository;

        public ChapterCatalogService(
            IChapterCatalogRepository chapterRepository,
            IStoryCatalogRepository storyRepository)
        {
            _chapterRepository = chapterRepository;
            _storyRepository = storyRepository;
        }

        public async Task<PagedResult<ChapterCatalogListItemResponse>> GetChaptersAsync(ChapterCatalogQuery query, CancellationToken ct = default)
        {
            if (query.Page < 1 || query.PageSize < 1)
            {
                throw new AppException("ValidationFailed", "Page and PageSize must be positive integers.", 400);
            }

            // Ensure story exists and is visible
            _ = await _storyRepository.GetPublishedStoryByIdAsync(query.StoryId, ct)
                ?? throw new AppException("StoryNotFound", "Story was not found or not available.", 404);

            var (chapters, total) = await _chapterRepository.GetPublishedChaptersByStoryAsync(query.StoryId, query.Page, query.PageSize, ct);

            var items = chapters.Select(ch => new ChapterCatalogListItemResponse
            {
                ChapterId = ch.chapter_id,
                ChapterNo = (int)ch.chapter_no,
                Title = ch.title,
                LanguageCode = ch.language?.lang_code ?? string.Empty,
                WordCount = ch.word_count,
                AccessType = ch.access_type,
                IsLocked = !string.Equals(ch.access_type, "free", StringComparison.OrdinalIgnoreCase),
                PublishedAt = ch.published_at
            }).ToList();

            return new PagedResult<ChapterCatalogListItemResponse>
            {
                Items = items,
                Total = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<ChapterCatalogDetailResponse> GetChapterAsync(Guid chapterId, CancellationToken ct = default)
        {
            var chapter = await _chapterRepository.GetPublishedChapterByIdAsync(chapterId, ct)
                           ?? throw new AppException("ChapterNotFound", "Chapter was not found or not available.", 404);

            var isLocked = !string.Equals(chapter.access_type, "free", StringComparison.OrdinalIgnoreCase);
            if (isLocked)
            {
                throw new AppException("ChapterLocked", "This chapter requires purchase to view.", 403);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Chapter content is not available.", 500);
            }

            return new ChapterCatalogDetailResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                LanguageCode = chapter.language?.lang_code ?? string.Empty,
                WordCount = chapter.word_count,
                AccessType = chapter.access_type,
                IsLocked = false,
                PublishedAt = chapter.published_at,
                ContentUrl = chapter.content_url
            };
        }
    }
}

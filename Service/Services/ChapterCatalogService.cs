using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ChapterCatalogService : IChapterCatalogService
    {
        private readonly IChapterCatalogRepository _chapterRepository;
        private readonly IStoryCatalogRepository _storyRepository;
        private readonly IChapterPurchaseRepository _chapterPurchaseRepository;

        public ChapterCatalogService(
            IChapterCatalogRepository chapterRepository,
            IStoryCatalogRepository storyRepository,
            IChapterPurchaseRepository chapterPurchaseRepository)
        {
            _chapterRepository = chapterRepository;
            _storyRepository = storyRepository;
            _chapterPurchaseRepository = chapterPurchaseRepository;
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

        public async Task<ChapterCatalogDetailResponse> GetChapterAsync(Guid chapterId, CancellationToken ct = default, Guid? viewerAccountId = null)
        {
            var chapter = await _chapterRepository.GetPublishedChapterByIdAsync(chapterId, ct)
                           ?? throw new AppException("ChapterNotFound", "Chapter was not found or not available.", 404);

            var isLocked = !string.Equals(chapter.access_type, "free", StringComparison.OrdinalIgnoreCase);
            if (isLocked)
            {
                var storyAuthorId = chapter.story?.author_id;
                var viewerIsAuthor = viewerAccountId.HasValue && storyAuthorId.HasValue && storyAuthorId.Value == viewerAccountId.Value;
                if (!viewerIsAuthor)
                {
                    if (!viewerAccountId.HasValue)
                    {
                        throw new AppException("ChapterLocked", "This chapter requires purchase to view.", 403);
                    }

                    var hasPurchased = await _chapterRepository.HasReaderPurchasedChapterAsync(chapterId, viewerAccountId.Value, ct);
                    if (!hasPurchased)
                    {
                        throw new AppException("ChapterLocked", "This chapter requires purchase to view.", 403);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Chapter content is not available.", 500);
            }

            var voices = Array.Empty<PurchasedVoiceResponse>();
            if (viewerAccountId.HasValue)
            {
                var purchasedVoices = await _chapterPurchaseRepository.GetPurchasedVoicesAsync(viewerAccountId.Value, chapterId, ct);
                if (purchasedVoices.Count > 0)
                {
                    voices = purchasedVoices.Select(v => new PurchasedVoiceResponse
                    {
                        PurchaseVoiceId = v.PurchaseVoiceId,
                        ChapterId = v.ChapterId,
                        StoryId = v.StoryId,
                        VoiceId = v.VoiceId,
                        VoiceName = v.VoiceName,
                        VoiceCode = v.VoiceCode,
                        PriceDias = (int)v.PriceDias,
                        AudioUrl = v.AudioUrl,
                        PurchasedAt = v.PurchasedAt
                    }).ToArray();
                }
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
                ContentUrl = chapter.content_url,
                Voices = voices
            };
        }

        public async Task<IReadOnlyList<ChapterCatalogVoiceResponse>> GetChapterVoicesAsync(Guid chapterId, Guid? viewerAccountId, CancellationToken ct = default)
        {
            var chapter = await _chapterRepository.GetPublishedChapterWithVoicesAsync(chapterId, ct)
                           ?? throw new AppException("ChapterNotFound", "Chapter was not found or not available.", 404);

            var ownedVoiceIds = Array.Empty<Guid>();
            if (viewerAccountId.HasValue)
            {
                ownedVoiceIds = (await _chapterPurchaseRepository.GetPurchasedVoiceIdsAsync(chapterId, viewerAccountId.Value, ct)).ToArray();
            }

            if (chapter.chapter_voices == null || chapter.chapter_voices.Count == 0)
            {
                return Array.Empty<ChapterCatalogVoiceResponse>();
            }

            var ownedSet = ownedVoiceIds.Length > 0 ? new HashSet<Guid>(ownedVoiceIds) : null;

            return chapter.chapter_voices
                .OrderBy(v => v.voice?.voice_name)
                .Select(v => new ChapterCatalogVoiceResponse
                {
                    VoiceId = v.voice_id,
                    VoiceName = v.voice?.voice_name ?? string.Empty,
                    VoiceCode = v.voice?.voice_code ?? string.Empty,
                    Status = v.status,
                    PriceDias = (int)v.dias_price,
                    HasAudio = string.Equals(v.status, "ready", StringComparison.OrdinalIgnoreCase),
                    Owned = ownedSet?.Contains(v.voice_id) ?? false
                })
                .ToArray();
        }
    }
}

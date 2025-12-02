using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ChapterCatalogService : IChapterCatalogService
    {
        private const string PremiumPlanCode = "premium_month";

        private readonly IChapterCatalogRepository _chapterRepository;
        private readonly IStoryCatalogRepository _storyRepository;
        private readonly IChapterPurchaseRepository _chapterPurchaseRepository;
        private readonly IMoodMusicRepository _moodMusicRepository;
        private readonly ISubscriptionService _subscriptionService;

        public ChapterCatalogService(
            IChapterCatalogRepository chapterRepository,
            IStoryCatalogRepository storyRepository,
            IChapterPurchaseRepository chapterPurchaseRepository,
            IMoodMusicRepository moodMusicRepository,
            ISubscriptionService subscriptionService)
        {
            _chapterRepository = chapterRepository;
            _storyRepository = storyRepository;
            _chapterPurchaseRepository = chapterPurchaseRepository;
            _moodMusicRepository = moodMusicRepository;
            _subscriptionService = subscriptionService;
        }

        public async Task<PagedResult<ChapterCatalogListItemResponse>> GetChaptersAsync(ChapterCatalogQuery query, CancellationToken ct = default)
        {
            if (query.Page < 1 || query.PageSize < 1)
            {
                throw new AppException("ValidationFailed", "Page and PageSize must be positive integers.", 400);
            }

            // Ensure story exists and is visible
            var story = await _storyRepository.GetPublishedStoryByIdAsync(query.StoryId, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found or not available.", 404);

            var (chapters, total) = await _chapterRepository.GetPublishedChaptersByStoryAsync(query.StoryId, query.Page, query.PageSize, ct);

            var viewerAccountId = query.ViewerAccountId;
            var viewerIsAuthor = viewerAccountId.HasValue && story.author_id == viewerAccountId.Value;

            var purchasedChapterIds = new HashSet<Guid>();
            if (viewerAccountId.HasValue && !viewerIsAuthor)
            {
                var hasLocked = chapters.Any(ch => !string.Equals(ch.access_type, "free", StringComparison.OrdinalIgnoreCase));
                if (hasLocked)
                {
                    var purchased = await _chapterPurchaseRepository.GetPurchasedChaptersAsync(viewerAccountId.Value, query.StoryId, ct);
                    if (purchased.Count > 0)
                    {
                        purchasedChapterIds = purchased.Select(p => p.ChapterId).ToHashSet();
                    }
                }
            }

            var items = chapters.Select(ch =>
            {
                var isLocked = !string.Equals(ch.access_type, "free", StringComparison.OrdinalIgnoreCase);
                var isOwned = viewerIsAuthor || (isLocked && purchasedChapterIds.Contains(ch.chapter_id));

                return new ChapterCatalogListItemResponse
                {
                    ChapterId = ch.chapter_id,
                    ChapterNo = (int)ch.chapter_no,
                    Title = ch.title,
                    LanguageCode = ch.language?.lang_code ?? string.Empty,
                    WordCount = ch.word_count,
                    AccessType = ch.access_type,
                    IsLocked = isLocked,
                    IsOwned = isOwned,
                    PriceDias = (int)ch.dias_price,
                    PublishedAt = ch.published_at
                };
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
            var isOwned = false;
            if (viewerAccountId.HasValue)
            {
                var storyAuthorId = chapter.story?.author_id;
                var viewerIsAuthor = storyAuthorId.HasValue && storyAuthorId.Value == viewerAccountId.Value;
                if (viewerIsAuthor)
                {
                    isOwned = true;
                }
                else if (isLocked)
                {
                    var hasPurchased = await _chapterRepository.HasReaderPurchasedChapterAsync(chapterId, viewerAccountId.Value, ct);
                    if (!hasPurchased)
                    {
                        throw new AppException("ChapterLocked", "This chapter requires purchase to view.", 403);
                    }
                    isOwned = true;
                }
            }
            else if (isLocked)
            {
                throw new AppException("ChapterLocked", "This chapter requires purchase to view.", 403);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Chapter content is not available.", 500);
            }

            var voices = Array.Empty<PurchasedVoiceResponse>();
            var moodResponse = (ChapterMoodResponse?)null;
            string[] moodMusicPaths = Array.Empty<string>();
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

                if (await HasPremiumAsync(viewerAccountId.Value, ct))
                {
                    (moodResponse, moodMusicPaths) = await ResolveMoodAsync(chapter, ct);
                }
            }
            else if (!isLocked)
            {
                // anonymous users cannot access mood music
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
                IsLocked = isLocked,
                IsOwned = !isLocked || isOwned,
                PriceDias = (int)chapter.dias_price,
                PublishedAt = chapter.published_at,
                ContentUrl = chapter.content_url,
                Mood = moodResponse,
                MoodMusicPaths = moodMusicPaths,
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
                    Owned = ownedSet?.Contains(v.voice_id) ?? false,
                    AudioUrl = (ownedSet?.Contains(v.voice_id) ?? false) && string.Equals(v.status, "ready", StringComparison.OrdinalIgnoreCase)
                        ? v.storage_path
                        : null
                })
                .ToArray();
        }

        public async Task<ChapterCatalogVoiceResponse> GetChapterVoiceAsync(Guid chapterId, Guid voiceId, Guid? viewerAccountId, CancellationToken ct = default)
        {
            var voice = await _chapterRepository.GetChapterVoiceAsync(chapterId, voiceId, ct)
                       ?? throw new AppException("VoiceNotFound", "Voice was not found for this chapter.", 404);

            var owned = false;
            if (viewerAccountId.HasValue)
            {
                var ownedVoiceIds = await _chapterPurchaseRepository.GetPurchasedVoiceIdsAsync(chapterId, viewerAccountId.Value, ct);
                if (ownedVoiceIds.Count > 0)
                {
                    owned = ownedVoiceIds.Contains(voiceId);
                }
            }

            var ready = string.Equals(voice.status, "ready", StringComparison.OrdinalIgnoreCase);

            return new ChapterCatalogVoiceResponse
            {
                VoiceId = voice.voice_id,
                VoiceName = voice.voice?.voice_name ?? string.Empty,
                VoiceCode = voice.voice?.voice_code ?? string.Empty,
                Status = voice.status,
                PriceDias = (int)voice.dias_price,
                HasAudio = ready,
                Owned = owned,
                AudioUrl = owned && ready ? voice.storage_path : null
            };
        }

        private async Task<(ChapterMoodResponse? Mood, string[] MusicPaths)> ResolveMoodAsync(chapter chapter, CancellationToken ct)
        {
            var moodCode = string.IsNullOrWhiteSpace(chapter.mood_code) ? "neutral" : chapter.mood_code!;
            var mood = await _moodMusicRepository.GetMoodAsync(moodCode, ct);
            if (mood == null)
            {
                return (null, Array.Empty<string>());
            }

            var tracks = await _moodMusicRepository.GetTracksByMoodAsync(mood.mood_code, ct);
            if (tracks.Count == 0 && !string.Equals(mood.mood_code, "neutral", StringComparison.OrdinalIgnoreCase))
            {
                tracks = await _moodMusicRepository.GetTracksByMoodAsync("neutral", ct);
            }

            var moodDto = new ChapterMoodResponse
            {
                Code = mood.mood_code,
                Name = mood.mood_name
            };

            var paths = tracks.Select(t => t.storage_path).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

            return (moodDto, paths);
        }

        private async Task<bool> HasPremiumAsync(Guid accountId, CancellationToken ct)
        {
            try
            {
                var status = await _subscriptionService.GetStatusAsync(accountId, ct);
                return status.HasActiveSubscription &&
                       string.Equals(status.PlanCode, PremiumPlanCode, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}

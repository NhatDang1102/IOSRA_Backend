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
                throw new AppException("ValidationFailed", "Page và PageSize phải là số nguyên dương.", 400);
            }

            // Ensure story exists and is visible
            var story = await _storyRepository.GetPublishedStoryByIdAsync(query.StoryId, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện hoặc truyện không khả dụng.", 404);

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
                    LanguageCode = ch.story?.language?.lang_code ?? string.Empty,
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
            var chapter = await _chapterRepository.GetPublishedChapterWithVoicesAsync(chapterId, ct)
                           ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương hoặc chương không khả dụng.", 404);

            var isLocked = !string.Equals(chapter.access_type, "free", StringComparison.OrdinalIgnoreCase);
            var isOwned = false;
            var viewerIsAuthor = false;
            if (viewerAccountId.HasValue)
            {
                var storyAuthorId = chapter.story?.author_id;
                viewerIsAuthor = storyAuthorId.HasValue && storyAuthorId.Value == viewerAccountId.Value;
                if (viewerIsAuthor)
                {
                    isOwned = true;
                }
                else if (isLocked)
                {
                    var hasPurchased = await _chapterRepository.HasReaderPurchasedChapterAsync(chapterId, viewerAccountId.Value, ct);
                    if (!hasPurchased)
                    {
                        throw new AppException("ChapterLocked", "Chương này yêu cầu mua để xem.", 403);
                    }
                    isOwned = true;
                }
            }
            else if (isLocked)
            {
                throw new AppException("ChapterLocked", "Chương này yêu cầu mua để xem.", 403);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Nội dung chương không khả dụng.", 500);
            }

            var voices = new List<PurchasedVoiceResponse>();
            var moodResponse = (ChapterMoodResponse?)null;
            var moodMusicPaths = Array.Empty<MoodMusicTrackResponse>();
            if (viewerAccountId.HasValue)
            {
                if (viewerIsAuthor)
                {
                    if (chapter.chapter_voices != null)
                    {
                        voices = chapter.chapter_voices
                            .Where(v => string.Equals(v.status, "ready", StringComparison.OrdinalIgnoreCase))
                            .Select(v => new PurchasedVoiceResponse
                            {
                                PurchaseVoiceId = Guid.Empty, // No purchase record for author
                                ChapterId = v.chapter_id,
                                StoryId = chapter.story_id,
                                VoiceId = v.voice_id,
                                VoiceName = v.voice?.voice_name ?? string.Empty,
                                VoiceCode = v.voice?.voice_code ?? string.Empty,
                                PriceDias = (int)v.dias_price,
                                AudioUrl = v.storage_path,
                                PurchasedAt = chapter.published_at ?? DateTime.UtcNow
                            }).ToList();
                    }
                }
                else
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
                        }).ToList();
                    }
                }

                if (viewerIsAuthor || await HasPremiumAsync(viewerAccountId.Value, ct))
                {
                    (moodResponse, moodMusicPaths) = await ResolveMoodAsync(chapter, ct);
                }
            }

            return new ChapterCatalogDetailResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                LanguageCode = chapter.story?.language?.lang_code ?? string.Empty,
                WordCount = chapter.word_count,
                AccessType = chapter.access_type,
                IsLocked = isLocked,
                IsOwned = !isLocked || isOwned,
                IsAuthor = viewerIsAuthor,
                PriceDias = (int)chapter.dias_price,
                PublishedAt = chapter.published_at,
                ContentUrl = chapter.content_url,
                Mood = moodResponse,
                MoodMusicPaths = moodMusicPaths,
                Voices = voices.ToArray()
            };
        }

        public async Task<IReadOnlyList<ChapterCatalogVoiceResponse>> GetChapterVoicesAsync(Guid chapterId, Guid? viewerAccountId, CancellationToken ct = default)
        {
            var chapter = await _chapterRepository.GetPublishedChapterWithVoicesAsync(chapterId, ct)
                           ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương hoặc chương không khả dụng.", 404);

            var viewerIsAuthor = viewerAccountId.HasValue && chapter.story?.author_id == viewerAccountId.Value;
            var ownedVoiceIds = new HashSet<Guid>();
            if (viewerAccountId.HasValue && !viewerIsAuthor)
            {
                var purchased = await _chapterPurchaseRepository.GetPurchasedVoiceIdsAsync(chapterId, viewerAccountId.Value, ct);
                ownedVoiceIds = new HashSet<Guid>(purchased);
            }

            if (chapter.chapter_voices == null || chapter.chapter_voices.Count == 0)
            {
                return Array.Empty<ChapterCatalogVoiceResponse>();
            }

            return chapter.chapter_voices
                .OrderBy(v => v.voice?.voice_name)
                .Select(v =>
                {
                    var isOwned = viewerIsAuthor || ownedVoiceIds.Contains(v.voice_id);
                    var isReady = string.Equals(v.status, "ready", StringComparison.OrdinalIgnoreCase);
                    return new ChapterCatalogVoiceResponse
                    {
                        VoiceId = v.voice_id,
                        VoiceName = v.voice?.voice_name ?? string.Empty,
                        VoiceCode = v.voice?.voice_code ?? string.Empty,
                        Status = v.status,
                        PriceDias = (int)v.dias_price,
                        HasAudio = isReady,
                        Owned = isOwned,
                        AudioUrl = isOwned && isReady ? v.storage_path : null
                    };
                })
                .ToArray();
        }

        public async Task<ChapterCatalogVoiceResponse> GetChapterVoiceAsync(Guid chapterId, Guid voiceId, Guid? viewerAccountId, CancellationToken ct = default)
        {
            var chapterVoice = await _chapterRepository.GetChapterVoiceAsync(chapterId, voiceId, ct)
                       ?? throw new AppException("VoiceNotFound", "Không tìm thấy giọng đọc cho chương này.", 404);

            var storyAuthorId = chapterVoice.chapter?.story?.author_id;
            var viewerIsAuthor = viewerAccountId.HasValue && storyAuthorId.HasValue && storyAuthorId.Value == viewerAccountId.Value;

            var owned = false;
            if (viewerAccountId.HasValue)
            {
                if (viewerIsAuthor)
                {
                    owned = true;
                }
                else
                {
                    var ownedVoiceIds = await _chapterPurchaseRepository.GetPurchasedVoiceIdsAsync(chapterId, viewerAccountId.Value, ct);
                    if (ownedVoiceIds.Count > 0)
                    {
                        owned = ownedVoiceIds.Contains(voiceId);
                    }
                }
            }

            var ready = string.Equals(chapterVoice.status, "ready", StringComparison.OrdinalIgnoreCase);

            return new ChapterCatalogVoiceResponse
            {
                VoiceId = chapterVoice.voice_id,
                VoiceName = chapterVoice.voice?.voice_name ?? string.Empty,
                VoiceCode = chapterVoice.voice?.voice_code ?? string.Empty,
                Status = chapterVoice.status,
                PriceDias = (int)chapterVoice.dias_price,
                HasAudio = ready,
                Owned = owned,
                AudioUrl = owned && ready ? chapterVoice.storage_path : null
            };
        }

        private async Task<(ChapterMoodResponse? Mood, MoodMusicTrackResponse[] Paths)> ResolveMoodAsync(chapter chapter, CancellationToken ct)
        {
            var moodCode = string.IsNullOrWhiteSpace(chapter.mood_code) ? "neutral" : chapter.mood_code!;
            var mood = await _moodMusicRepository.GetMoodAsync(moodCode, ct);
            if (mood == null)
            {
                return (null, Array.Empty<MoodMusicTrackResponse>());
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

            var paths = tracks
                .Select(t => new MoodMusicTrackResponse
                {
                    Title = t.title,
                    StoragePath = t.storage_path
                })
                .Where(t => !string.IsNullOrWhiteSpace(t.StoragePath))
                .ToArray();

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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ChapterTranslationService : IChapterTranslationService
    {
        private const string PremiumPlanCode = "premium_month";

        private readonly IChapterCatalogRepository _chapterRepository;
        private readonly IChapterContentStorage _contentStorage;
        private readonly IOpenAiTranslationService _translationService;
        private readonly ISubscriptionService _subscriptionService;

        private static readonly Regex WordRegex = new Regex(@"\b[\p{L}\p{Nd}_']+\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ChapterTranslationService(
            IChapterCatalogRepository chapterRepository,
            IChapterContentStorage contentStorage,
            IOpenAiTranslationService translationService,
            ISubscriptionService subscriptionService)
        {
            _chapterRepository = chapterRepository;
            _contentStorage = contentStorage;
            _translationService = translationService;
            _subscriptionService = subscriptionService;
        }

        public async Task<ChapterTranslationResponse> TranslateAsync(Guid chapterId, ChapterTranslationRequest request, Guid requesterAccountId, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetLanguageCode))
            {
                throw new AppException("ValidationFailed", "Cần chọn ngôn ngữ muốn dịch.", 400);
            }

            var chapter = await LoadPublishedChapterAsync(chapterId, ct);
            await EnsureChapterReadableAsync(chapter, requesterAccountId, ct);
            await EnsurePremiumSubscriptionAsync(requesterAccountId, ct);

            var targetLanguage = await GetLanguageOrThrowAsync(request.TargetLanguageCode, ct);
            EnsureNotOriginalLanguage(chapter, targetLanguage);

            var existingLocalization = await _chapterRepository.GetLocalizationAsync(chapter.chapter_id, targetLanguage.lang_id, ct);
            if (existingLocalization != null)
            {
                throw new AppException("TranslationExists", "Đã có bản dịch cho ngôn ngữ này rồi.", 409);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "nội dung chap bị trống.", 500);
            }

            var originalContent = await _contentStorage.DownloadAsync(chapter.content_url, ct);
            if (string.IsNullOrWhiteSpace(originalContent))
            {
                throw new AppException("ChapterContentEmpty", "nội dung chap bị trống.", 400);
            }

            var translated = await _translationService.TranslateAsync(
                originalContent,
                chapter.story?.language?.lang_code ?? "vi-VN",
                targetLanguage.lang_code,
                ct);

            if (string.IsNullOrWhiteSpace(translated))
            {
                throw new AppException("TranslationFailed", "có lỗi trong quá trình dịch.", 500);
            }

            var wordCount = Math.Max(1, CountWords(translated));
            var storageKey = await _contentStorage.UploadLocalizationAsync(chapter.story_id, chapter.chapter_id, targetLanguage.lang_code, translated, ct);

            var localization = new chapter_localization
            {
                chapter_id = chapter.chapter_id,
                lang_id = targetLanguage.lang_id,
                word_count = (uint)wordCount,
                content_url = storageKey
            };

            await _chapterRepository.AddLocalizationAsync(localization, ct);
            await _chapterRepository.SaveChangesAsync(ct);

            return MapResponse(chapter, targetLanguage, localization, cached: false);
        }

        public async Task<ChapterTranslationResponse> GetAsync(Guid chapterId, string languageCode, Guid? viewerAccountId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                throw new AppException("ValidationFailed", "Thiếu mã ngôn ngữ.", 400);
            }

            if (!viewerAccountId.HasValue)
            {
                throw new AppException("SubscriptionRequired", "Bạn cần gói premium_month để xem bản dịch.", 403);
            }

            await EnsurePremiumSubscriptionAsync(viewerAccountId.Value, ct);

            var chapter = await LoadPublishedChapterAsync(chapterId, ct);
            await EnsureChapterReadableAsync(chapter, viewerAccountId, ct);

            var targetLanguage = await GetLanguageOrThrowAsync(languageCode, ct);
            EnsureNotOriginalLanguage(chapter, targetLanguage);

            var localization = await _chapterRepository.GetLocalizationAsync(chapter.chapter_id, targetLanguage.lang_id, ct)
                               ?? throw new AppException("TranslationNotFound", "Chapter chưa được dịch.", 404);

            return MapResponse(chapter, targetLanguage, localization, cached: true);
        }

        public async Task<ChapterTranslationStatusResponse> GetStatusesAsync(Guid chapterId, Guid? viewerAccountId, CancellationToken ct = default)
        {
            if (!viewerAccountId.HasValue)
            {
                throw new AppException("SubscriptionRequired", "Bạn cần gói premium_month để xem trạng thái dịch.", 403);
            }

            await EnsurePremiumSubscriptionAsync(viewerAccountId.Value, ct);

            var chapter = await LoadPublishedChapterAsync(chapterId, ct);
            await EnsureChapterReadableAsync(chapter, viewerAccountId, ct);

            var languages = await _chapterRepository.GetLanguagesAsync(ct);
            var localizations = await _chapterRepository.GetLocalizationsByChapterAsync(chapter.chapter_id, ct);
            var localizationMap = localizations.ToDictionary(l => l.lang_id, l => l);

            var originalCode = chapter.story?.language?.lang_code ?? string.Empty;
            var locales = new ChapterTranslationLocaleStatus[languages.Count];

            for (var i = 0; i < languages.Count; i++)
            {
                var language = languages[i];
                localizationMap.TryGetValue(language.lang_id, out var localization);
                var isOriginal = string.Equals(language.lang_code, originalCode, StringComparison.OrdinalIgnoreCase);

                locales[i] = new ChapterTranslationLocaleStatus
                {
                    LanguageCode = language.lang_code,
                    LanguageName = language.lang_name ?? language.lang_code,
                    IsOriginal = isOriginal,
                    HasTranslation = isOriginal || localization != null,
                    ContentUrl = isOriginal ? chapter.content_url : localization?.content_url,
                    WordCount = isOriginal ? (int)chapter.word_count : (int)(localization?.word_count ?? 0)
                };
            }

            return new ChapterTranslationStatusResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                OriginalLanguageCode = originalCode,
                Locales = locales
            };
        }

        private async Task<chapter> LoadPublishedChapterAsync(Guid chapterId, CancellationToken ct)
        {
            var chapter = await _chapterRepository.GetPublishedChapterByIdAsync(chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Chương không tồn tại.", 404);

            if (chapter.story == null)
            {
                throw new AppException("StoryNotFound", "Chương không có truyện hợp lệ.", 500);
            }

            return chapter;
        }

        private async Task EnsureChapterReadableAsync(chapter chapter, Guid? viewerAccountId, CancellationToken ct)
        {
            var isLocked = string.Equals(chapter.access_type, "dias", StringComparison.OrdinalIgnoreCase);
            if (!isLocked)
            {
                return;
            }

            var viewerIsAuthor = viewerAccountId.HasValue && chapter.story?.author_id == viewerAccountId.Value;
            if (viewerIsAuthor)
            {
                return;
            }

            if (!viewerAccountId.HasValue)
            {
                throw new AppException("ChapterLocked", "Bạn cần mua chương này trước.", 403);
            }

            var hasPurchased = await _chapterRepository.HasReaderPurchasedChapterAsync(chapter.chapter_id, viewerAccountId.Value, ct);
            if (!hasPurchased)
            {
                throw new AppException("ChapterLocked", "Bạn cần mua chương này trước.", 403);
            }
        }

        private async Task<language_list> GetLanguageOrThrowAsync(string languageCode, CancellationToken ct)
        {
            var language = await _chapterRepository.GetLanguageByCodeAsync(languageCode.Trim(), ct)
                          ?? throw new AppException("LanguageNotFound", "Ngôn ngữ không được hỗ trợ.", 404);
            return language;
        }

        private static void EnsureNotOriginalLanguage(chapter chapter, language_list targetLanguage)
        {
            var originalCode = chapter.story?.language?.lang_code ?? string.Empty;
            if (string.Equals(originalCode, targetLanguage.lang_code, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("TranslationNotNeeded", "Chương đã có sẵn ngôn ngữ này.", 400);
            }
        }

        private static int CountWords(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            return WordRegex.Matches(content).Count;
        }

        private ChapterTranslationResponse MapResponse(chapter chapter, language_list targetLanguage, chapter_localization localization, bool cached)
        {
            return new ChapterTranslationResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                OriginalLanguageCode = chapter.story?.language?.lang_code ?? string.Empty,
                TargetLanguageCode = targetLanguage.lang_code,
                TargetLanguageName = targetLanguage.lang_name ?? targetLanguage.lang_code,
                ContentUrl = localization.content_url,
                WordCount = (int)localization.word_count,
                Cached = cached
            };
        }

        private async Task EnsurePremiumSubscriptionAsync(Guid accountId, CancellationToken ct)
        {
            var status = await _subscriptionService.GetStatusAsync(accountId, ct);
            if (!status.HasActiveSubscription || !string.Equals(status.PlanCode, PremiumPlanCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("SubscriptionRequired", "Bạn cần gói premium_month để sử dụng tính năng dịch chương.", 403);
            }
        }
    }
}
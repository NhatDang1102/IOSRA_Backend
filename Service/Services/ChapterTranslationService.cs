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
                throw new AppException("ValidationFailed", "Target language is required.", 400);
            }

            var chapter = await LoadPublishedChapterAsync(chapterId, ct);
            await EnsureChapterReadableAsync(chapter, requesterAccountId, ct);

            var subscriptionStatus = await _subscriptionService.GetStatusAsync(requesterAccountId, ct);
            if (!subscriptionStatus.HasActiveSubscription)
            {
                throw new AppException("SubscriptionRequired", "Bạn cần gói subscription để dịch chương này.", 403);
            }

            var targetLanguage = await GetLanguageOrThrowAsync(request.TargetLanguageCode, ct);
            EnsureNotOriginalLanguage(chapter, targetLanguage);

            var existingLocalization = await _chapterRepository.GetLocalizationAsync(chapter.chapter_id, targetLanguage.lang_id, ct);
            if (existingLocalization != null)
            {
                throw new AppException("TranslationExists", "Ngôn ngữ này đã tồn tại.", 409);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new AppException("ChapterContentMissing", "Chapter content is missing.", 500);
            }

            var originalContent = await _contentStorage.DownloadAsync(chapter.content_url, ct);
            if (string.IsNullOrWhiteSpace(originalContent))
            {
                throw new AppException("ChapterContentEmpty", "Chapter content is empty.", 400);
            }

            var translated = await _translationService.TranslateAsync(
                originalContent,
                chapter.language?.lang_code ?? "vi-VN",
                targetLanguage.lang_code,
                ct);

            if (string.IsNullOrWhiteSpace(translated))
            {
                throw new AppException("TranslationFailed", "Không thể dịch chương này.", 500);
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

            return MapResponse(chapter, targetLanguage, localization, translated, cached: false);
        }

        public async Task<ChapterTranslationResponse> GetAsync(Guid chapterId, string languageCode, Guid? viewerAccountId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                throw new AppException("ValidationFailed", "Language code is required.", 400);
            }

            var chapter = await LoadPublishedChapterAsync(chapterId, ct);
            await EnsureChapterReadableAsync(chapter, viewerAccountId, ct);

            var targetLanguage = await GetLanguageOrThrowAsync(languageCode, ct);
            EnsureNotOriginalLanguage(chapter, targetLanguage);

            var localization = await _chapterRepository.GetLocalizationAsync(chapter.chapter_id, targetLanguage.lang_id, ct)
                               ?? throw new AppException("TranslationNotFound", "Chapter nA?y ch??a cA3 b???n d??<ch, hA?y d??<ch tr????>c.", 404);

            var content = await LoadLocalizationContentAsync(localization, ct);
            return MapResponse(chapter, targetLanguage, localization, content, cached: true);
        }

        public async Task<ChapterTranslationStatusResponse> GetStatusesAsync(Guid chapterId, Guid? viewerAccountId, CancellationToken ct = default)
        {
            var chapter = await LoadPublishedChapterAsync(chapterId, ct);
            await EnsureChapterReadableAsync(chapter, viewerAccountId, ct);

            var languages = await _chapterRepository.GetLanguagesAsync(ct);
            var localizations = await _chapterRepository.GetLocalizationsByChapterAsync(chapter.chapter_id, ct);
            var localizationMap = new Dictionary<Guid, chapter_localization>();
            foreach (var localization in localizations)
            {
                localizationMap[localization.lang_id] = localization;
            }

            var originalCode = chapter.language?.lang_code ?? string.Empty;
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
                          ?? throw new AppException("ChapterNotFound", "Chapter was not found or not available.", 404);

            if (chapter.story == null)
            {
                throw new AppException("StoryNotFound", "Story info missing.", 500);
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
                          ?? throw new AppException("LanguageNotFound", "Ngôn ngữ này không được hỗ trợ.", 404);
            return language;
        }

        private static void EnsureNotOriginalLanguage(chapter chapter, language_list targetLanguage)
        {
            var originalCode = chapter.language?.lang_code ?? string.Empty;
            if (string.Equals(originalCode, targetLanguage.lang_code, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("TranslationNotNeeded", "Chapter đã ở ngôn ngữ này.", 400);
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

        private async Task<string> LoadLocalizationContentAsync(chapter_localization localization, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(localization.content_url))
            {
                throw new AppException("TranslationFileMissing", "Translation content not found.", 500);
            }

            var content = await _contentStorage.DownloadAsync(localization.content_url, ct);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AppException("TranslationFileMissing", "Translation content is empty.", 500);
            }

            return content;
        }

        private ChapterTranslationResponse MapResponse(chapter chapter, language_list targetLanguage, chapter_localization localization, string content, bool cached)
        {
            return new ChapterTranslationResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                OriginalLanguageCode = chapter.language?.lang_code ?? string.Empty,
                TargetLanguageCode = targetLanguage.lang_code,
                TargetLanguageName = targetLanguage.lang_name ?? targetLanguage.lang_code,
                Content = content,
                ContentUrl = localization.content_url,
                WordCount = (int)localization.word_count,
                Cached = cached
            };
        }
    }
}


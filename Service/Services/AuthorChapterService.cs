using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Voice;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Service.Helpers;
using Contract.DTOs.Response.Chapter;

namespace Service.Services
{
    public class AuthorChapterService : IAuthorChapterService
    {
        private const int MinContentLength = 50;
        private readonly IAuthorChapterRepository _chapterRepository;
        private readonly IAuthorStoryRepository _storyRepository;
        private readonly IChapterContentStorage _contentStorage;
        private readonly IOpenAiModerationService _openAiModerationService;
        private readonly IFollowerNotificationService _followerNotificationService;
        private readonly IChapterPricingService _chapterPricingService;
        private readonly IProfileRepository? _profileRepository;

        private static readonly string[] AuthorChapterAllowedStatuses = { "draft", "pending", "rejected", "published", "hidden", "removed" };
        private static readonly string[] ChapterAccessTypes = { "free", "dias" };

        public AuthorChapterService(
            IAuthorChapterRepository chapterRepository,
            IAuthorStoryRepository storyRepository,
            IChapterContentStorage contentStorage,
            IOpenAiModerationService openAiModerationService,
            IFollowerNotificationService followerNotificationService,
            IChapterPricingService chapterPricingService,
            IProfileRepository? profileRepository = null)
        {
            _chapterRepository = chapterRepository;
            _storyRepository = storyRepository;
            _contentStorage = contentStorage;
            _openAiModerationService = openAiModerationService;
            _followerNotificationService = followerNotificationService;
            _chapterPricingService = chapterPricingService;
            _profileRepository = profileRepository;
        }

        public async Task<ChapterResponse> CreateAsync(Guid authorAccountId, Guid storyId, ChapterCreateRequest request, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);

            await AccountRestrictionHelper.EnsureCanPublishAsync(author.account, _profileRepository, ct);

            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            if (!string.Equals(story.status, "published", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(story.status, "hidden", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryNotPublished", "Chapters can only be created when the story is published or hidden.", 400);
            }

            var lastRejectedAt = await _chapterRepository.GetLastAuthorChapterRejectedAtAsync(author.account_id, ct);
            //if (lastRejectedAt.HasValue && lastRejectedAt.Value > TimezoneConverter.VietnamNow.AddHours(-24))
            //{
            //    throw new AppException("ChapterCreationCooldown", "You must wait 24 hours after a chapter rejection before creating a new chapter.", 400, new
            //    {
            //        availableAtUtc = lastRejectedAt.Value.AddHours(24)
            //    });
            //}

            if (await _chapterRepository.StoryHasPendingChapterAsync(story.story_id, ct))
            {
                throw new AppException("ChapterPendingExists", "A chapter is already awaiting moderation for this story.", 400);
            }

            var title = (request.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new AppException("InvalidChapterTitle", "Chapter title must not be empty.", 400);
            }

            var languageCode = (request.LanguageCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(languageCode))
            {
                throw new AppException("LanguageCodeRequired", "Language code must not be empty.", 400);
            }

            var language = await _chapterRepository.GetLanguageByCodeAsync(languageCode, ct)
                          ?? throw new AppException("LanguageNotSupported", $"Language '{languageCode}' is not supported.", 400);
            var content = (request.Content ?? string.Empty).Trim();
            if (content.Length < MinContentLength)
            {
                throw new AppException("ChapterContentTooShort", $"Chapter content must contain at least {MinContentLength} characters.", 400);
            }

            var wordCount = CountWords(content);
            if (wordCount <= 0)
            {
                throw new AppException("ChapterContentEmpty", "Chapter content must include words.", 400);
            }

            var charCount = content.Length;
            var price = await _chapterPricingService.GetPriceAsync(wordCount, ct);
            var chapterNumber = await _chapterRepository.GetNextChapterNumberAsync(story.story_id, ct);
            var chapterId = Guid.NewGuid();
            var requestedAccessType = NormalizeOptionalChapterAccessType(request.AccessType);
            var canLockChapters = CanAuthorLockChapters(author, story);
            var accessType = ResolveAccessTypeForCreate(requestedAccessType, canLockChapters, story.is_premium);

            var chapter = new chapter
            {
                chapter_id = chapterId,
                story_id = story.story_id,
                chapter_no = (uint)chapterNumber,
                language_id = language.lang_id,
                title = title,
                summary = null,
                dias_price = (uint)price,
                access_type = accessType,
                content_url = null,
                word_count = wordCount,
                char_count = charCount,
                status = "draft",
                created_at = TimezoneConverter.VietnamNow,
                updated_at = TimezoneConverter.VietnamNow,
                submitted_at = null,
                published_at = null
            };
            chapter.language = language;

            string contentKey;
            try
            {
                contentKey = await _contentStorage.UploadAsync(story.story_id, chapter.chapter_id, content, ct);
                chapter.content_url = contentKey;
                await _chapterRepository.AddAsync(chapter, ct);
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(chapter.content_url))
                {
                    await _contentStorage.DeleteAsync(chapter.content_url, ct);
                }
                throw;
            }

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        public async Task<IReadOnlyList<ChapterListItemResponse>> ListAsync(Guid authorAccountId, Guid storyId, string? status, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);

            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            var filterStatuses = NormalizeChapterStatuses(status);
            var chapters = await _chapterRepository.GetByStoryAsync(story.story_id, filterStatuses, ct);
            return chapters.Select(MapChapterListItem).ToArray();
        }

        public async Task<ChapterResponse> GetAsync(Guid authorAccountId, Guid storyId, Guid chapterId, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);

            var chapter = await _chapterRepository.GetForAuthorAsync(storyId, chapterId, author.account_id, ct)
                          ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        public async Task<ChapterResponse> SubmitAsync(Guid authorAccountId, Guid chapterId, ChapterSubmitRequest request, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);

            var chapter = await _chapterRepository.GetByIdAsync(chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            if (story.author_id != author.account_id)
            {
                throw new AppException("ChapterNotFound", "Chapter was not found.", 404);
            }

            if (string.Equals(story.status, "hidden", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryHidden", "Story is hidden. Please restore it before submitting chapters.", 400);
            }

            if (await _chapterRepository.StoryHasPendingChapterAsync(story.story_id, ct) &&
                !string.Equals(chapter.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterPendingExists", "Another chapter is already awaiting moderation.", 400);
            }

            if (!string.Equals(chapter.status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidChapterState", "Only draft chapters can be submitted. Create a new chapter if the previous one was rejected.", 400);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new InvalidOperationException("Chapter content is missing.");
            }

            var content = await _contentStorage.DownloadAsync(chapter.content_url, ct);
            var moderation = await _openAiModerationService.ModerateChapterAsync(chapter.title, content, ct);
            var aiScoreDecimal = (decimal)Math.Round(moderation.Score, 2, MidpointRounding.AwayFromZero);
            var timestamp = TimezoneConverter.VietnamNow;

            chapter.updated_at = timestamp;
            chapter.submitted_at = timestamp;

            var shouldReject = moderation.ShouldReject || aiScoreDecimal < 5m;
            var autoApprove = !shouldReject && aiScoreDecimal >= 7m;
            var notifyFollowers = false;

            if (shouldReject)
            {
                chapter.status = "rejected";
                chapter.published_at = null;
                await _chapterRepository.UpdateAsync(chapter, ct);

                var rejectionApproval = await UpsertChapterApprovalAsync(chapter, "rejected", aiScoreDecimal, moderation.Explanation, ct);

                throw new AppException("ChapterRejectedByAi", "Chapter was rejected by automated moderation.", 400, new
                {
                    reviewId = rejectionApproval.review_id,
                    score = Math.Round(moderation.Score, 2),
                    explanation = moderation.Explanation,
                    violations = moderation.Violations.Select(v => new { v.Word, v.Count, v.Samples })
                });
            }

            if (autoApprove)
            {
                chapter.status = "published";
                chapter.published_at ??= TimezoneConverter.VietnamNow;
                await _chapterRepository.UpdateAsync(chapter, ct);
                await UpsertChapterApprovalAsync(chapter, "approved", aiScoreDecimal, moderation.Explanation, ct);
                notifyFollowers = true;
            }
            else
            {
                chapter.status = "pending";
                chapter.published_at = null;
                await _chapterRepository.UpdateAsync(chapter, ct);

                await UpsertChapterApprovalAsync(chapter, "pending", aiScoreDecimal, moderation.Explanation, ct);
            }

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);

            if (notifyFollowers)
            {
                var authorName = story.author?.account.username ?? "TÃƒÆ’Ã‚Â¡c giÃƒÂ¡Ã‚ÂºÃ‚Â£";
                await _followerNotificationService.NotifyChapterPublishedAsync(
                    story.author_id,
                    authorName,
                    story.story_id,
                    story.title,
                    chapter.chapter_id,
                    chapter.title,
                    (int)chapter.chapter_no,
                    ct);
            }

            return MapChapter(chapter, approvals);
        }

        public async Task<ChapterResponse> WithdrawAsync(Guid authorAccountId, Guid chapterId, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);

            var chapter = await _chapterRepository.GetByIdAsync(chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            _ = await _storyRepository.GetStoryForAuthorAsync(chapter.story_id, author.account_id, ct)
                ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            if (!string.Equals(chapter.status, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("WithdrawNotAllowed", "Only rejected chapters can be withdrawn.", 400);
            }

            chapter.status = "draft";
            chapter.submitted_at = null;
            chapter.published_at = null;
            chapter.updated_at = TimezoneConverter.VietnamNow;
            await _chapterRepository.UpdateAsync(chapter, ct);

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        public async Task<ChapterResponse> UpdateDraftAsync(Guid authorAccountId, Guid storyId, Guid chapterId, ChapterUpdateRequest request, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);

            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            var chapter = await _chapterRepository.GetForAuthorAsync(story.story_id, chapterId, author.account_id, ct)
                         ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            if (!string.Equals(chapter.status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterUpdateNotAllowed", "Only draft chapters can be edited before submission.", 400);
            }

            var updated = false;

            if (request.Title != null)
            {
                var title = request.Title.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    throw new AppException("InvalidChapterTitle", "Chapter title must not be empty.", 400);
                }

                chapter.title = title;
                updated = true;
            }

            if (request.LanguageCode != null)
            {
                var languageCode = request.LanguageCode.Trim();
                if (string.IsNullOrWhiteSpace(languageCode))
                {
                    throw new AppException("LanguageCodeRequired", "Language code must not be empty.", 400);
                }

                var language = await _chapterRepository.GetLanguageByCodeAsync(languageCode, ct)
                              ?? throw new AppException("LanguageNotSupported", $"Language '{languageCode}' is not supported.", 400);
                chapter.language_id = language.lang_id;
                chapter.language = language;
                updated = true;
            }

            if (request.Content != null)
            {
                var content = request.Content.Trim();
                if (content.Length < MinContentLength)
                {
                    throw new AppException("ChapterContentTooShort", $"Chapter content must contain at least {MinContentLength} characters.", 400);
                }

                var wordCount = CountWords(content);
                if (wordCount <= 0)
                {
                    throw new AppException("ChapterContentEmpty", "Chapter content must include words.", 400);
                }

                var charCount = content.Length;
                var price = await _chapterPricingService.GetPriceAsync(wordCount, ct);
                var previousContentKey = chapter.content_url;
                string? newContentKey = null;
                try
                {
                    newContentKey = await _contentStorage.UploadAsync(story.story_id, chapter.chapter_id, content, ct);

                    var shouldDeleteOld = !string.IsNullOrWhiteSpace(previousContentKey)
                        && !string.Equals(previousContentKey, newContentKey, StringComparison.Ordinal);
                    if (shouldDeleteOld)
                    {
                        await _contentStorage.DeleteAsync(previousContentKey!, ct);
                    }

                    chapter.content_url = newContentKey;
                }
                catch
                {
                    var shouldDeleteNew = !string.IsNullOrWhiteSpace(newContentKey)
                        && (string.IsNullOrWhiteSpace(previousContentKey)
                            || !string.Equals(previousContentKey, newContentKey, StringComparison.Ordinal));
                    if (shouldDeleteNew)
                    {
                        await _contentStorage.DeleteAsync(newContentKey!, ct);
                    }
                    throw;
                }

                chapter.word_count = wordCount;
                chapter.char_count = charCount;
                chapter.dias_price = (uint)price;
                updated = true;
            }

            if (request.AccessType != null)
            {
                var normalizedAccessType = NormalizeOptionalChapterAccessType(request.AccessType)
                                           ?? throw new AppException("InvalidAccessType", "Access type is required when provided.", 400);

                var canLockChapters = CanAuthorLockChapters(author, story);
                if (string.Equals(normalizedAccessType, "dias", StringComparison.OrdinalIgnoreCase) && !canLockChapters)
                {
                    throw new AppException("AccessTypeNotAllowed", "Only higher-ranked authors can lock chapters.", 400);
                }

                if (!string.Equals(chapter.access_type, normalizedAccessType, StringComparison.OrdinalIgnoreCase))
                {
                    chapter.access_type = normalizedAccessType;
                    updated = true;
                }
            }

            if (!updated)
            {
                throw new AppException("NoChanges", "No changes were provided.", 400);
            }

            chapter.updated_at = TimezoneConverter.VietnamNow;
            await _chapterRepository.UpdateAsync(chapter, ct);

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        private static IReadOnlyList<string>? NormalizeChapterStatuses(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var normalized = status.Trim();
            if (!AuthorChapterAllowedStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Unsupported status '{status}'. Allowed values are: {string.Join(", ", AuthorChapterAllowedStatuses)}.", 400);
            }

            return new[] { normalized.ToLowerInvariant() };
        }

        private static string? NormalizeOptionalChapterAccessType(string? accessType)
        {
            if (accessType == null)
            {
                return null;
            }

            var normalized = accessType.Trim().ToLowerInvariant();
            if (normalized.Length == 0)
            {
                throw new AppException("InvalidAccessType", "Access type must be 'free' or 'dias'.", 400);
            }

            if (!ChapterAccessTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidAccessType", $"Access type '{accessType}' is not supported. Allowed values are: {string.Join(", ", ChapterAccessTypes)}.", 400);
            }

            return normalized;
        }

        private static bool CanAuthorLockChapters(author author, story story)
        {
            if (story.is_premium)
            {
                return true;
            }

            var rankName = author.rank?.rank_name;
            return !string.IsNullOrWhiteSpace(rankName) &&
                   !string.Equals(rankName, "casual", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveAccessTypeForCreate(string? requestedAccessType, bool canLockChapters, bool storyIsPremium)
        {
            var defaultAccess = storyIsPremium ? "dias" : "free";
            if (requestedAccessType == null)
            {
                return canLockChapters ? defaultAccess : "free";
            }

            if (string.Equals(requestedAccessType, "dias", StringComparison.OrdinalIgnoreCase) && !canLockChapters)
            {
                throw new AppException("AccessTypeNotAllowed", "Only higher-ranked authors can lock chapters.", 400);
            }

            return requestedAccessType;
        }

        private static ChapterResponse MapChapter(chapter chapter, IReadOnlyList<content_approve> approvals)
        {
            var language = chapter.language ?? throw new InvalidOperationException("Chapter language navigation was not loaded.");
            var latestApproval = approvals?
                .OrderByDescending(a => a.created_at)
                .FirstOrDefault();
            var moderatorStatus = latestApproval?.moderator_id.HasValue == true ? latestApproval.status : null;
            var moderatorNote = latestApproval?.moderator_id.HasValue == true ? latestApproval.moderator_feedback : null;

            return new ChapterResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                Summary = chapter.summary,
                WordCount = chapter.word_count,
                CharCount = chapter.char_count,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                PriceDias = (int)chapter.dias_price,
                AccessType = chapter.access_type,
                Status = chapter.status,
                AiScore = latestApproval?.ai_score,
                AiFeedback = latestApproval?.ai_feedback,
                AiResult = ResolveAiDecision(latestApproval),
                ModeratorStatus = moderatorStatus,
                ModeratorNote = moderatorNote,
                ContentPath = chapter.content_url,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at,
                SubmittedAt = chapter.submitted_at,
                PublishedAt = chapter.published_at,
                Voices = MapVoices(chapter)
            };
        }

        private static VoiceChapterVoiceResponse[] MapVoices(chapter chapter)
        {
            if (chapter.chapter_voices == null || chapter.chapter_voices.Count == 0)
            {
                return Array.Empty<VoiceChapterVoiceResponse>();
            }

            return chapter.chapter_voices
                .OrderBy(v => v.voice?.voice_name)
                .Select(v => new VoiceChapterVoiceResponse
                {
                    VoiceId = v.voice_id,
                    VoiceName = v.voice?.voice_name ?? string.Empty,
                    VoiceCode = v.voice?.voice_code ?? string.Empty,
                    Status = v.status,
                    AudioUrl = v.storage_path,
                    RequestedAt = v.requested_at,
                    CompletedAt = v.completed_at,
                    CharCost = v.char_cost,
                    ErrorMessage = v.error_message
                })
                .ToArray();
        }

        private static ChapterListItemResponse MapChapterListItem(chapter chapter)
        {
            var language = chapter.language ?? throw new InvalidOperationException("Chapter language navigation was not loaded.");
            var approval = chapter.content_approves?
                .OrderByDescending(a => a.created_at)
                .FirstOrDefault();
            var moderatorStatus = approval?.moderator_id.HasValue == true ? approval.status : null;
            var moderatorNote = approval?.moderator_id.HasValue == true ? approval.moderator_feedback : null;

            return new ChapterListItemResponse
            {
                ChapterId = chapter.chapter_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                WordCount = chapter.word_count,
                CharCount = chapter.char_count,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                PriceDias = (int)chapter.dias_price,
                Status = chapter.status,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at,
                SubmittedAt = chapter.submitted_at,
                PublishedAt = chapter.published_at,
                AiScore = approval?.ai_score,
                AiResult = ResolveAiDecision(approval),
                AiFeedback = approval?.ai_feedback,
                ModeratorStatus = moderatorStatus,
                ModeratorNote = moderatorNote
            };
        }

        private static string? ResolveAiDecision(content_approve? approval)
        {
            if (approval == null || approval.ai_score is not decimal score)
            {
                return null;
            }

            if (score < 5m)
            {
                return "rejected";
            }

            if (score >= 7m)
            {
                return "approved";
            }

            return "flagged";
        }

        private async Task<content_approve> UpsertChapterApprovalAsync(chapter chapter, string status, decimal aiScore, string? aiNote, CancellationToken ct)
        {
            var approval = await _chapterRepository.GetContentApprovalForChapterAsync(chapter.chapter_id, ct);
            var timestamp = TimezoneConverter.VietnamNow;

            if (approval == null)
            {
                approval = new content_approve
                {
                    approve_type = "chapter",
                    story_id = chapter.story_id,
                    chapter_id = chapter.chapter_id,
                    status = status,
                    ai_score = aiScore,
                    ai_feedback = aiNote,
                    moderator_feedback = null,
                    moderator_id = null,
                    created_at = timestamp
                };

                await _chapterRepository.AddContentApproveAsync(approval, ct);
            }
            else
            {
                approval.status = status;
                approval.ai_score = aiScore;
                approval.ai_feedback = aiNote;
                approval.moderator_feedback = null;
                approval.moderator_id = null;
                approval.created_at = timestamp;

                await _chapterRepository.SaveChangesAsync(ct);
            }

            return approval;
        }

        private static int CountWords(string content)
        {
            var matches = Regex.Matches(content, @"[\p{L}\p{N}']+");
            return matches.Count;
        }

    }
}







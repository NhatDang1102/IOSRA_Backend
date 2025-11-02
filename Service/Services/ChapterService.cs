using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Services
{
    public class ChapterService : IChapterService
    {
        private const int MinContentLength = 50;
        private readonly IChapterRepository _chapterRepository;
        private readonly IStoryRepository _storyRepository;
        private readonly IChapterContentStorage _contentStorage;
        private readonly IOpenAiModerationService _openAiModerationService;

        private static readonly string[] AuthorChapterAllowedStatuses = { "draft", "pending", "rejected", "published", "hidden", "removed" };

        public ChapterService(
            IChapterRepository chapterRepository,
            IStoryRepository storyRepository,
            IChapterContentStorage contentStorage,
            IOpenAiModerationService openAiModerationService)
        {
            _chapterRepository = chapterRepository;
            _storyRepository = storyRepository;
            _contentStorage = contentStorage;
            _openAiModerationService = openAiModerationService;
        }

        public async Task<ChapterResponse> CreateAsync(Guid authorAccountId, Guid storyId, ChapterCreateRequest request, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);

            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            if (!string.Equals(story.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryNotPublished", "Chapters can only be created for published stories.", 400);
            }

            var lastRejectedAt = await _chapterRepository.GetLastAuthorChapterRejectedAtAsync(author.account_id, ct);
            if (lastRejectedAt.HasValue && lastRejectedAt.Value > DateTime.UtcNow.AddHours(-24))
            {
                throw new AppException("ChapterCreationCooldown", "You must wait 24 hours after a chapter rejection before creating a new chapter.", 400, new
                {
                    availableAtUtc = lastRejectedAt.Value.AddHours(24)
                });
            }

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

            var price = CalculatePrice(wordCount);
            var chapterNumber = await _chapterRepository.GetNextChapterNumberAsync(story.story_id, ct);
            var chapterId = Guid.NewGuid();
            var accessType = story.is_premium ? "coin" : "free";

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
                ai_score = null,
                ai_feedback = null,
                status = "draft",
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow,
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
            var timestamp = DateTime.UtcNow;

            chapter.updated_at = timestamp;
            chapter.ai_score = aiScoreDecimal;
            chapter.ai_feedback = moderation.Explanation;
            chapter.submitted_at = timestamp;

            var shouldReject = moderation.ShouldReject || aiScoreDecimal < 0.50m;
            var autoApprove = !shouldReject && aiScoreDecimal >= 0.70m;

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
                chapter.published_at ??= DateTime.UtcNow;
                await _chapterRepository.UpdateAsync(chapter, ct);
                await UpsertChapterApprovalAsync(chapter, "approved", aiScoreDecimal, moderation.Explanation, ct);
            }
            else
            {
                chapter.status = "pending";
                chapter.published_at = null;
                chapter.ai_feedback = moderation.Explanation;
                await _chapterRepository.UpdateAsync(chapter, ct);

                await UpsertChapterApprovalAsync(chapter, "pending", aiScoreDecimal, moderation.Explanation, ct);
            }

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

        private static ChapterResponse MapChapter(chapter chapter, IReadOnlyList<content_approve> approvals)
        {
            var language = chapter.language ?? throw new InvalidOperationException("Chapter language navigation was not loaded.");

            return new ChapterResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                Summary = chapter.summary,
                WordCount = chapter.word_count,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                PriceDias = (int)chapter.dias_price,
                AccessType = chapter.access_type,
                Status = chapter.status,
                AiScore = chapter.ai_score,
                AiFeedback = chapter.ai_feedback,
                ContentPath = chapter.content_url,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at,
                SubmittedAt = chapter.submitted_at,
                PublishedAt = chapter.published_at
            };
        }

        private static ChapterListItemResponse MapChapterListItem(chapter chapter)
        {
            var language = chapter.language ?? throw new InvalidOperationException("Chapter language navigation was not loaded.");

            return new ChapterListItemResponse
            {
                ChapterId = chapter.chapter_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                WordCount = chapter.word_count,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                PriceDias = (int)chapter.dias_price,
                Status = chapter.status,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at,
                SubmittedAt = chapter.submitted_at,
                PublishedAt = chapter.published_at
            };
        }

        private async Task<content_approve> UpsertChapterApprovalAsync(chapter chapter, string status, decimal aiScore, string? aiNote, CancellationToken ct)
        {
            var approval = await _chapterRepository.GetContentApprovalForChapterAsync(chapter.chapter_id, ct);
            var timestamp = DateTime.UtcNow;

            if (approval == null)
            {
                approval = new content_approve
                {
                    approve_type = "chapter",
                    story_id = chapter.story_id,
                    chapter_id = chapter.chapter_id,
                    status = status,
                    ai_score = aiScore,
                    ai_note = aiNote,
                    moderator_note = null,
                    moderator_id = null,
                    created_at = timestamp
                };

                await _chapterRepository.AddContentApproveAsync(approval, ct);
            }
            else
            {
                approval.status = status;
                approval.ai_score = aiScore;
                approval.ai_note = aiNote;
                approval.moderator_note = null;
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

        private static int CalculatePrice(int wordCount)
        {
            if (wordCount <= 3000) return 5;
            if (wordCount <= 4000) return 6;
            if (wordCount <= 5000) return 7;
            if (wordCount <= 6000) return 8;
            if (wordCount <= 7000) return 9;
            return 10;
        }
    }
}







using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Services
{
    public class ChapterModerationService : IChapterModerationService
    {
        private readonly IChapterModerationRepository _chapterRepository;
        private readonly IMailSender _mailSender;

        public ChapterModerationService(IChapterModerationRepository chapterRepository, IMailSender mailSender)
        {
            _chapterRepository = chapterRepository;
            _mailSender = mailSender;
        }

        private static readonly string[] AllowedStatuses = { "pending", "published", "rejected" };

        public async Task<IReadOnlyList<ChapterModerationQueueItem>> ListAsync(string? status, CancellationToken ct = default)
        {
            var statuses = NormalizeStatuses(status);
            var chapters = await _chapterRepository.GetForModerationAsync(statuses, ct);
            var response = new List<ChapterModerationQueueItem>(chapters.Count);

            foreach (var chapter in chapters)
            {
                var review = chapter.content_approves?
                    .OrderByDescending(c => c.created_at)
                    .FirstOrDefault();
                if (review == null)
                {
                    continue;
                }

                response.Add(MapQueueItem(chapter, review));
            }

            return response;
        }

        public async Task<ChapterModerationQueueItem> GetAsync(Guid reviewId, CancellationToken ct = default)
        {
            var approval = await _chapterRepository.GetContentApprovalByIdAsync(reviewId, ct)
                           ?? throw new AppException("ModerationRequestNotFound", "Moderation request was not found.", 404);

            if (!string.Equals(approval.approve_type, "chapter", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidModerationType", "Moderation request is not associated with a chapter.", 400);
            }

            var chapter = approval.chapter ?? throw new InvalidOperationException("Chapter navigation was not loaded for moderation entry.");
            return MapQueueItem(chapter, approval);
        }

        public async Task ModerateAsync(Guid moderatorAccountId, Guid reviewId, ChapterModerationDecisionRequest request, CancellationToken ct = default)
        {
            var approval = await _chapterRepository.GetContentApprovalByIdAsync(reviewId, ct)
                           ?? throw new AppException("ModerationRequestNotFound", "Moderation request was not found.", 404);

            if (!string.Equals(approval.approve_type, "chapter", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidModerationType", "Moderation request is not associated with a chapter.", 400);
            }

            if (!string.Equals(approval.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ModerationAlreadyHandled", "This moderation request has already been processed.", 400);
            }

            var chapter = approval.chapter ?? throw new InvalidOperationException("Chapter navigation was not loaded for moderation entry.");
            if (!string.Equals(chapter.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotPending", "Chapter is not awaiting moderation.", 400);
            }

            approval.status = request.Approve ? "approved" : "rejected";
            var humanNote = string.IsNullOrWhiteSpace(request.ModeratorNote) ? null : request.ModeratorNote.Trim();
            approval.moderator_feedback = humanNote;
            approval.moderator_id = moderatorAccountId;
            approval.created_at = TimezoneConverter.VietnamNow;

            if (request.Approve)
            {
                chapter.status = "published";
                chapter.published_at ??= TimezoneConverter.VietnamNow;
            }
            else
            {
                chapter.status = "rejected";
                chapter.published_at = null;
                if (!string.IsNullOrWhiteSpace(humanNote))
                {
                approval.ai_feedback = humanNote;
            }
            }

            chapter.updated_at = TimezoneConverter.VietnamNow;

            await _chapterRepository.SaveChangesAsync(ct);

            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            var author = story.author ?? throw new InvalidOperationException("Story author navigation was not loaded.");
            var authorAccount = author.account ?? throw new InvalidOperationException("Author account navigation was not loaded.");

            if (request.Approve)
            {
                await _mailSender.SendChapterApprovedEmailAsync(authorAccount.email, story.title, chapter.title);
            }
            else
            {
                await _mailSender.SendChapterRejectedEmailAsync(authorAccount.email, story.title, chapter.title, approval.moderator_feedback);
            }
        }

        private static IReadOnlyList<string> NormalizeStatuses(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return AllowedStatuses;
            }

            var normalized = status.Trim();
            if (!AllowedStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Unsupported status '{status}'. Allowed values are: {string.Join(", ", AllowedStatuses)}.", 400);
            }

            return new[] { normalized.ToLowerInvariant() };
        }

        private static ChapterModerationQueueItem MapQueueItem(chapter chapter, content_approve review)
        {
            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            var author = story.author ?? throw new InvalidOperationException("Story author navigation was not loaded.");
            var account = author.account ?? throw new InvalidOperationException("Author account navigation was not loaded.");
            var language = chapter.language ?? throw new InvalidOperationException("Chapter language navigation was not loaded.");
            var aiScore = review.ai_score;
            var aiFeedback = review.ai_feedback;

            var submittedAt = chapter.submitted_at ?? (review.created_at == default ? chapter.updated_at : review.created_at);

            return new ChapterModerationQueueItem
            {
                ReviewId = review.review_id,
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                StoryTitle = story.title,
                ChapterTitle = chapter.title,
                AuthorId = author.account_id,
                AuthorUsername = account.username,
                AuthorEmail = account.email,
                ChapterNo = (int)chapter.chapter_no,
                WordCount = chapter.word_count,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                PriceDias = (int)chapter.dias_price,
                AiScore = aiScore,
                AiFeedback = aiFeedback,
                Status = chapter.status,
                SubmittedAt = submittedAt,
                CreatedAt = chapter.created_at
            };
        }
    }
}



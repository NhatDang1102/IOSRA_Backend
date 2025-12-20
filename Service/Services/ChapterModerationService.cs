using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Constants;
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
        private readonly INotificationService _notificationService;
        private readonly IFollowerNotificationService _followerNotificationService;
        private readonly IContentModRepository _contentModRepository;

        public ChapterModerationService(
            IChapterModerationRepository chapterRepository,
            IMailSender mailSender,
            INotificationService notificationService,
            IFollowerNotificationService followerNotificationService,
            IContentModRepository contentModRepository)
        {
            _chapterRepository = chapterRepository;
            _mailSender = mailSender;
            _notificationService = notificationService;
            _followerNotificationService = followerNotificationService;
            _contentModRepository = contentModRepository;
        }

        private static readonly string[] AllowedStatuses = { "pending", "published", "rejected" };
        //get all list chapter trong content approve 
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
                           ?? throw new AppException("ModerationRequestNotFound", "Không tìm thấy yêu cầu kiểm duyệt.", 404);

            if (!string.Equals(approval.approve_type, "chapter", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidModerationType", "Yêu cầu kiểm duyệt không liên quan đến chương.", 400);
            }

            var chapter = approval.chapter ?? throw new InvalidOperationException("Chapter navigation was not loaded for moderation entry.");
            return MapQueueItem(chapter, approval);
        }
        //quyết định kiểm duyệt 
        public async Task ModerateAsync(Guid moderatorAccountId, Guid reviewId, ChapterModerationDecisionRequest request, CancellationToken ct = default)
        {
            var approval = await _chapterRepository.GetContentApprovalByIdAsync(reviewId, ct)
                           ?? throw new AppException("ModerationRequestNotFound", "Không tìm thấy yêu cầu kiểm duyệt.", 404);

            if (!string.Equals(approval.approve_type, "chapter", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidModerationType", "Yêu cầu kiểm duyệt không liên quan đến chương.", 400);
            }

            if (!string.Equals(approval.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ModerationAlreadyHandled", "Yêu cầu kiểm duyệt này đã được xử lý.", 400);
            }

            var chapter = approval.chapter ?? throw new InvalidOperationException("Chapter navigation was not loaded for moderation entry.");
            if (!string.Equals(chapter.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotPending", "Chương không ở trạng thái chờ kiểm duyệt.", 400);
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
            }

            chapter.updated_at = TimezoneConverter.VietnamNow;

            await _chapterRepository.SaveChangesAsync(ct);
            await _contentModRepository.IncrementChapterDecisionAsync(moderatorAccountId, request.Approve, ct);

            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            var author = story.author ?? throw new InvalidOperationException("Story author navigation was not loaded.");
            var authorAccount = author.account ?? throw new InvalidOperationException("Author account navigation was not loaded.");

            var statusText = request.Approve ? "approved" : "rejected";

            if (request.Approve)
            {
                await _mailSender.SendChapterApprovedEmailAsync(authorAccount.email, story.title, chapter.title);
            }
            else
            {
                await _mailSender.SendChapterRejectedEmailAsync(authorAccount.email, story.title, chapter.title, approval.moderator_feedback);
            }

            var title = request.Approve
                ? $"Chương \"{chapter.title}\" đã được duyệt"
                : $"Chương \"{chapter.title}\" bị từ chối";

            var message = request.Approve
                ? $"Ban kiểm duyệt đã phê duyệt chương \"{chapter.title}\" thuộc truyện \"{story.title}\"."
                : string.IsNullOrWhiteSpace(humanNote)
                    ? $"Ban kiểm duyệt đã từ chối chương \"{chapter.title}\". Vui lòng kiểm tra lại nội dung."
                    : $"Ban kiểm duyệt đã từ chối chương \"{chapter.title}\": {humanNote}";

            await _notificationService.CreateAsync(new NotificationCreateModel(
                authorAccount.account_id,
                NotificationTypes.ChapterDecision,
                title,
                message,
                new
                {
                    reviewId = approval.review_id,
                    storyId = story.story_id,
                    chapterId = chapter.chapter_id,
                    status = statusText,
                    moderatorNote = humanNote
                }), ct);

            if (request.Approve)
            {
                var authorName = authorAccount.username;
                await _followerNotificationService.NotifyChapterPublishedAsync(
                    authorAccount.account_id,
                    authorName,
                    story.story_id,
                    story.title,
                    chapter.chapter_id,
                    chapter.title,
                    (int)chapter.chapter_no,
                    ct);
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
                throw new AppException("InvalidStatus", $"Trạng thái '{status}' không được hỗ trợ. Các giá trị cho phép là: {string.Join(", ", AllowedStatuses)}.", 400);
            }

            return new[] { normalized.ToLowerInvariant() };
        }

        private static ChapterModerationQueueItem MapQueueItem(chapter chapter, content_approve review)
        {
            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            var author = story.author ?? throw new InvalidOperationException("Story author navigation was not loaded.");
            var account = author.account ?? throw new InvalidOperationException("Author account navigation was not loaded.");
            var language = story.language ?? throw new InvalidOperationException("Story language navigation was not loaded.");
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
                ContentPath = chapter.content_url,
                AiScore = aiScore,
                AiFeedback = aiFeedback,
                AiResult = ResolveAiDecision(review),
                Status = chapter.status,
                SubmittedAt = submittedAt,
                CreatedAt = chapter.created_at
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
    }
}
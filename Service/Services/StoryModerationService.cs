using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Story;
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
    public class StoryModerationService : IStoryModerationService
    {
        private readonly IStoryModerationRepository _storyRepository;
        private readonly IMailSender _mailSender;
        private readonly INotificationService _notificationService;
        private readonly IFollowerNotificationService _followerNotificationService;
        private readonly IContentModRepository _contentModRepository;

        public StoryModerationService(
            IStoryModerationRepository storyRepository,
            IMailSender mailSender,
            INotificationService notificationService,
            IFollowerNotificationService followerNotificationService,
            IContentModRepository contentModRepository)
        {
            _storyRepository = storyRepository;
            _mailSender = mailSender;
            _notificationService = notificationService;
            _followerNotificationService = followerNotificationService;
            _contentModRepository = contentModRepository;
        }

        private static readonly string[] AllowedStatuses = { "pending", "published", "rejected" };

        public async Task<IReadOnlyList<StoryModerationQueueItem>> ListAsync(string? status, CancellationToken ct = default)
        {
            //ép response status thành chữ thường để so sánh
            var statuses = NormalizeStatuses(status);
            var stories = await _storyRepository.GetStoriesForModerationAsync(statuses, ct);
            var response = new List<StoryModerationQueueItem>(stories.Count);

            foreach (var story in stories)
            {
                var approval = story.content_approves?
                    .Where(a => string.Equals(a.approve_type, "story", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => c.created_at)
                    .FirstOrDefault();

                if (approval == null)
                {
                    continue;
                }

                response.Add(MapQueueItem(story, approval));
            }

            return response;
        }

        public async Task<StoryModerationQueueItem> GetAsync(Guid reviewId, CancellationToken ct = default)
        {
            var approval = await _storyRepository.GetContentApprovalByIdAsync(reviewId, ct)
                           ?? throw new AppException("ModerationRequestNotFound", "Không tìm thấy yêu cầu kiểm duyệt.", 404);

            if (!string.Equals(approval.approve_type, "story", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidModerationType", "Yêu cầu kiểm duyệt không liên quan đến truyện.", 400);
            }

            var story = approval.story ?? throw new InvalidOperationException("Story navigation was not loaded for moderation entry.");
            return MapQueueItem(story, approval);
        }

        public async Task ModerateAsync(Guid moderatorAccountId, Guid reviewId, StoryModerationDecisionRequest request, CancellationToken ct = default)
        {
            var approval = await _storyRepository.GetContentApprovalByIdAsync(reviewId, ct)
                           ?? throw new AppException("ModerationRequestNotFound", "Không tìm thấy yêu cầu kiểm duyệt.", 404);

            if (!string.Equals(approval.approve_type, "story", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidModerationType", "Yêu cầu kiểm duyệt không liên quan đến truyện.", 400);
            }

            if (!string.Equals(approval.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ModerationAlreadyHandled", "Yêu cầu kiểm duyệt này đã được xử lý.", 400);
            }

            var story = approval.story ?? throw new InvalidOperationException("Story navigation was not loaded for moderation entry.");
            if (story.status != "pending")
            {
                throw new AppException("StoryNotPending", "Truyện không ở trạng thái chờ kiểm duyệt.", 400);
            }

            var wasPublished = string.Equals(story.status, "published", StringComparison.OrdinalIgnoreCase);
            approval.status = request.Approve ? "approved" : "rejected";
            var humanNote = string.IsNullOrWhiteSpace(request.ModeratorNote) ? null : request.ModeratorNote.Trim();
            approval.moderator_feedback = humanNote;
            approval.moderator_id = moderatorAccountId;
            approval.created_at = TimezoneConverter.VietnamNow;

            if (request.Approve)
            {
                story.status = "published";
                story.published_at ??= TimezoneConverter.VietnamNow;
                var authorRank = story.author.rank?.rank_name;
                story.is_premium = !string.IsNullOrWhiteSpace(authorRank) && !string.Equals(authorRank, "Casual", StringComparison.OrdinalIgnoreCase);
                if (!wasPublished)
                {
                    var storyAuthor = story.author ?? throw new InvalidOperationException("Story author navigation was not loaded.");
                    storyAuthor.total_story += 1;
                }
            }
            else
            {
                story.status = "rejected";
                story.published_at = null;
            }
            story.updated_at = TimezoneConverter.VietnamNow;

            await _storyRepository.SaveChangesAsync(ct);
            await _contentModRepository.IncrementStoryDecisionAsync(moderatorAccountId, request.Approve, ct);

            var authorAccount = story.author.account;
            var authorEmail = authorAccount.email;
            var statusText = request.Approve ? "approved" : "rejected";

            if (request.Approve)
            {
                await _mailSender.SendStoryApprovedEmailAsync(authorEmail, story.title);
            }
            else
            {
                await _mailSender.SendStoryRejectedEmailAsync(authorEmail, story.title, approval.moderator_feedback);
            }

            var title = request.Approve
                ? $"Truyện \"{story.title}\" đã được duyệt"
                : $"Truyện \"{story.title}\" bị từ chối";

            var message = request.Approve
                ? "Ban kiểm duyệt đã phê duyệt truyện của bạn. Bạn có thể tiếp tục đăng chương mới."
                : string.IsNullOrWhiteSpace(humanNote)
                    ? "Ban kiểm duyệt đã từ chối truyện của bạn. Vui lòng kiểm tra lại nội dung."
                    : $"Ban kiểm duyệt đã từ chối truyện của bạn: {humanNote}";

            await _notificationService.CreateAsync(new NotificationCreateModel(
                authorAccount.account_id,
                NotificationTypes.StoryDecision,
                title,
                message,
                new
                {
                    reviewId = approval.review_id,
                    storyId = story.story_id,
                    status = statusText,
                    moderatorNote = humanNote
                }), ct);

            if (request.Approve)
            {
                var authorName = authorAccount.username;
                await _followerNotificationService.NotifyStoryPublishedAsync(authorAccount.account_id, authorName, story.story_id, story.title, ct);
            }
        }

        private static IReadOnlyList<string> NormalizeStatuses(string? status)
        {
            //check coi status hợp lệ ko
            if (string.IsNullOrWhiteSpace(status))
            {
                return AllowedStatuses;
            }

            //trim status lại
            var normalized = status.Trim();
            if (!AllowedStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Trạng thái '{status}' không được hỗ trợ. Các giá trị cho phép là: {string.Join(", ", AllowedStatuses)}.", 400);
            }

            return new[] { normalized.ToLowerInvariant() };
        }

        private static StoryModerationQueueItem MapQueueItem(story story, content_approve approval)
        {
            var tags = story.story_tags?
                .Where(st => st.tag != null)
                .Select(st => new StoryTagResponse
                {
                    TagId = st.tag_id,
                    TagName = st.tag.tag_name
                })
                .OrderBy(t => t.TagName, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<StoryTagResponse>();

            return new StoryModerationQueueItem
            {
                ReviewId = approval.review_id,
                StoryId = story.story_id,
                Title = story.title,
                LanguageCode = story.language?.lang_code,
                LanguageName = story.language?.lang_name,
                Description = story.desc,
                AuthorId = story.author_id,
                AuthorUsername = story.author.account.username,
                CoverUrl = story.cover_url,
                AiScore = approval.ai_score,
                AiFeedback = approval.ai_feedback,
                AiResult = ResolveAiDecision(approval),
                Status = story.status,
                Outline = story.outline,
                LengthPlan = story.length_plan,
                SubmittedAt = approval.created_at,
                PendingNote = approval.moderator_feedback,
                Tags = tags
            };
        }

        private static string? ResolveAiDecision(content_approve? approval)
        {
            if (approval == null)
            {
                return null;
            }

            if (approval.ai_score is not decimal score)
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
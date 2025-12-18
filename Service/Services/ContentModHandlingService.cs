using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Moderation;
using Contract.DTOs.Response.Moderation;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ContentModHandlingService : IContentModHandlingService
    {
        private static readonly string[] StoryAllowedStatuses = { "hidden", "published", "completed" };
        private static readonly string[] ChapterAllowedStatuses = { "hidden", "published" };
        private static readonly string[] CommentAllowedStatuses = { "visible", "hidden" };
        private static readonly IReadOnlyDictionary<int, TimeSpan> StrikeDurations = new Dictionary<int, TimeSpan>
        {
            { 1, TimeSpan.FromDays(1) },
            { 2, TimeSpan.FromDays(3) },
            { 3, TimeSpan.FromDays(30) },
            { 4, TimeSpan.FromDays(365 * 100) }
        };
        private static readonly IReadOnlyDictionary<int, string> StrikeDurationLabels = new Dictionary<int, string>
        {
            { 1, "1 day" },
            { 2, "3 days" },
            { 3, "30 days" },
            { 4, "100 years" }
        };

        private readonly IModerationRepository _moderationRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly IMailSender _mailSender;

        public ContentModHandlingService(
            IModerationRepository moderationRepository,
            IProfileRepository profileRepository,
            IMailSender mailSender)
        {
            _moderationRepository = moderationRepository;
            _profileRepository = profileRepository;
            _mailSender = mailSender;
        }

        public async Task<ModerationStatusResponse> UpdateStoryStatusAsync(Guid moderatorAccountId, Guid storyId, ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(request.Status, StoryAllowedStatuses, "Story");
            var story = await _moderationRepository.GetStoryAsync(storyId, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            story.status = normalizedStatus;
            story.updated_at = TimezoneConverter.VietnamNow;

            await _moderationRepository.UpdateStoryAsync(story, ct);
            return BuildResponse("story", storyId, normalizedStatus);
        }

        public async Task<ModerationStatusResponse> UpdateChapterStatusAsync(Guid moderatorAccountId, Guid chapterId, ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(request.Status, ChapterAllowedStatuses, "Chapter");
            var chapter = await _moderationRepository.GetChapterAsync(chapterId, ct)
                         ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            chapter.status = normalizedStatus;
            chapter.updated_at = TimezoneConverter.VietnamNow;

            await _moderationRepository.UpdateChapterAsync(chapter, ct);
            return BuildResponse("chapter", chapterId, normalizedStatus);
        }

        public async Task<ModerationStatusResponse> UpdateCommentStatusAsync(Guid moderatorAccountId, Guid commentId, ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(request.Status, CommentAllowedStatuses, "Comment");
            var comment = await _moderationRepository.GetCommentAsync(commentId, ct)
                         ?? throw new AppException("CommentNotFound", "Không tìm thấy bình luận.", 404);

            comment.status = normalizedStatus;
            comment.updated_at = TimezoneConverter.VietnamNow;

            await _moderationRepository.UpdateCommentAsync(comment, ct);
            return BuildResponse("comment", commentId, normalizedStatus);
        }

        public async Task ApplyStrikeAsync(Guid targetAccountId, StrikeLevelUpdateRequest request, CancellationToken ct = default)
        {
            if (!StrikeDurations.TryGetValue(request.Level, out var duration))
            {
                throw new AppException("InvalidStrikeLevel", "Mức cảnh cáo phải từ 1 đến 4.", 400);
            }

            var account = await _profileRepository.GetAccountByIdAsync(targetAccountId, ct)
                          ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404);

            var now = TimezoneConverter.VietnamNow;
            var baseStart = account.strike_restricted_until.HasValue && account.strike_restricted_until > now
                ? account.strike_restricted_until.Value
                : now;
            var restrictedUntil = baseStart.Add(duration);
            var highestLevel = (byte)Math.Max(account.strike, request.Level);

            await _profileRepository.UpdateStrikeAsync(
                account.account_id,
                highestLevel,
                ReportStrikeStatus.Restricted,
                restrictedUntil,
                ct);

            var durationLabel = StrikeDurationLabels[request.Level];
            var reasonText = $"A moderator applied strike level {request.Level} (account restricted for {durationLabel}). Please review and follow IOSRA policies.";
            await _mailSender.SendStrikeWarningEmailAsync(
                account.email,
                string.IsNullOrWhiteSpace(account.username) ? account.email : account.username,
                reasonText,
                highestLevel,
                restrictedUntil);
        }

        private static string NormalizeStatus(string? value, string[] allowed, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidStatus", $"Trạng thái {label} là bắt buộc.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Trạng thái {label} '{value}' không được hỗ trợ.", 400);
            }

            return normalized;
        }

        private static ModerationStatusResponse BuildResponse(string targetType, Guid targetId, string status)
            => new ModerationStatusResponse
            {
                TargetType = targetType,
                TargetId = targetId,
                Status = status
            };

        private static class ReportStrikeStatus
        {
            public const string Restricted = "restricted";
        }
    }
}
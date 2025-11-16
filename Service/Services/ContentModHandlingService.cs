using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Moderation;
using Contract.DTOs.Respond.Moderation;
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

        private readonly IModerationRepository _moderationRepository;
        private readonly IProfileRepository _profileRepository;

        public ContentModHandlingService(
            IModerationRepository moderationRepository,
            IProfileRepository profileRepository)
        {
            _moderationRepository = moderationRepository;
            _profileRepository = profileRepository;
        }

        public async Task<ModerationStatusResponse> UpdateStoryStatusAsync(Guid moderatorAccountId, Guid storyId, ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(request.Status, StoryAllowedStatuses, "Story");
            var story = await _moderationRepository.GetStoryAsync(storyId, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            story.status = normalizedStatus;
            story.updated_at = TimezoneConverter.VietnamNow;

            await _moderationRepository.UpdateStoryAsync(story, ct);
            return BuildResponse("story", storyId, normalizedStatus);
        }

        public async Task<ModerationStatusResponse> UpdateChapterStatusAsync(Guid moderatorAccountId, Guid chapterId, ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(request.Status, ChapterAllowedStatuses, "Chapter");
            var chapter = await _moderationRepository.GetChapterAsync(chapterId, ct)
                         ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            chapter.status = normalizedStatus;
            chapter.updated_at = TimezoneConverter.VietnamNow;

            await _moderationRepository.UpdateChapterAsync(chapter, ct);
            return BuildResponse("chapter", chapterId, normalizedStatus);
        }

        public async Task<ModerationStatusResponse> UpdateCommentStatusAsync(Guid moderatorAccountId, Guid commentId, ContentStatusUpdateRequest request, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(request.Status, CommentAllowedStatuses, "Comment");
            var comment = await _moderationRepository.GetCommentAsync(commentId, ct)
                         ?? throw new AppException("CommentNotFound", "Comment was not found.", 404);

            comment.status = normalizedStatus;
            comment.updated_at = TimezoneConverter.VietnamNow;

            await _moderationRepository.UpdateCommentAsync(comment, ct);
            return BuildResponse("comment", commentId, normalizedStatus);
        }

        public async Task OverrideStrikeAsync(Guid targetAccountId, StrikeStatusUpdateRequest request, CancellationToken ct = default)
        {
            if (request.Strike > 3)
            {
                throw new AppException("InvalidStrike", "Strike must be between 0 and 3.", 400);
            }

            var account = await _profileRepository.GetAccountByIdAsync(targetAccountId, ct)
                          ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            var normalizedStatus = NormalizeStrikeStatus(request.Status);
            DateTime? restrictedUntil = request.RestrictedUntil;

            if (string.Equals(normalizedStatus, ReportStrikeStatus.Restricted, StringComparison.OrdinalIgnoreCase))
            {
                if (!restrictedUntil.HasValue)
                {
                    throw new AppException("RestrictedUntilRequired", "restrictedUntil is required when status is restricted.", 400);
                }
            }
            else
            {
                restrictedUntil = null;
            }

            await _profileRepository.UpdateStrikeAsync(account.account_id, request.Strike, normalizedStatus, restrictedUntil, ct);
        }

        private static string NormalizeStatus(string? value, string[] allowed, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidStatus", $"{label} status is required.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"{label} status '{value}' is not supported.", 400);
            }

            return normalized;
        }

        private static string NormalizeStrikeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                throw new AppException("InvalidStrikeStatus", "Strike status is required.", 400);
            }

            var normalized = status.Trim().ToLowerInvariant();
            if (normalized != ReportStrikeStatus.None && normalized != ReportStrikeStatus.Restricted)
            {
                throw new AppException("InvalidStrikeStatus", $"Unsupported strike status '{status}'.", 400);
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
            public const string None = "none";
            public const string Restricted = "restricted";
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Report;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Report;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Constants;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ReportService : IReportService
    {
        private const int MaxPageSize = 100;

        private readonly IReportRepository _reportRepository;
        private readonly IModerationRepository _moderationRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly IMailSender _mailSender;
        private readonly INotificationService _notificationService;
        private readonly IContentModRepository _contentModRepository;

        public ReportService(
            IReportRepository reportRepository,
            IModerationRepository moderationRepository,
            IProfileRepository profileRepository,
            IMailSender mailSender,
            INotificationService notificationService,
            IContentModRepository contentModRepository)
        {
            _reportRepository = reportRepository;
            _moderationRepository = moderationRepository;
            _profileRepository = profileRepository;
            _mailSender = mailSender;
            _notificationService = notificationService;
            _contentModRepository = contentModRepository;
        }

        public async Task<ReportResponse> CreateAsync(Guid reporterAccountId, ReportCreateRequest request, CancellationToken ct = default)
        {
            var normalizedTargetType = NormalizeTargetType(request.TargetType);
            var normalizedReason = NormalizeReason(request.Reason);
            var targetContext = await GetTargetContextAsync(normalizedTargetType, request.TargetId, ct);

            var ownerAccount = targetContext.TargetAccount
                               ?? throw new AppException("TargetOwnerNotFound", "Không tìm thấy chủ sở hữu nội dung báo cáo.", 404);
            if (ownerAccount.account_id == reporterAccountId)
            {
                throw new AppException("CannotReportOwnContent", "Bạn không thể report nội dung của chính mình.", 400);
            }

            var alreadyPending = await _reportRepository.HasPendingReportAsync(reporterAccountId, normalizedTargetType, request.TargetId, ct);
            if (alreadyPending)
            {
                throw new AppException("ReportAlreadyExists", "Bạn đã có báo cáo đang chờ xử lý cho mục tiêu này.", 400);
            }

            var now = TimezoneConverter.VietnamNow;
            var entity = new report
            {
                report_id = Guid.NewGuid(),
                reporter_id = reporterAccountId,
                target_type = normalizedTargetType,
                target_id = request.TargetId,
                reason = normalizedReason,
                details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
                status = ReportStatuses.Pending,
                created_at = now,
                reviewed_at = null,
                moderator_id = null
            };

            await _reportRepository.AddAsync(entity, ct);
            var saved = await _reportRepository.GetByIdAsync(entity.report_id, ct)
                        ?? throw new InvalidOperationException("Report not found after creation.");
            return Map(saved);
        }

        public async Task<PagedResult<ReportResponse>> ListAsync(string? status, string? targetType, Guid? targetId, int page, int pageSize, CancellationToken ct = default)
        {
            var normalizedPage = NormalizePage(page);
            var normalizedSize = NormalizePageSize(pageSize);
            var normalizedStatus = NormalizeStatusFilter(status);
            var normalizedTargetType = NormalizeTargetTypeFilter(targetType);
            var normalizedTargetId = NormalizeGuidFilter(targetId);

            var (items, total) = await _reportRepository.GetPagedAsync(normalizedStatus, normalizedTargetType, normalizedTargetId, normalizedPage, normalizedSize, ct);
            var responses = items.Select(Map).ToArray();
            foreach (var response in responses)
            {
                await AttachTargetDetailsAsync(response, response.TargetType, response.TargetId, ct);
            }

            return new PagedResult<ReportResponse>
            {
                Items = responses,
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        public async Task<ReportResponse> GetAsync(Guid reportId, CancellationToken ct = default)
        {
            var entity = await _reportRepository.GetByIdAsync(reportId, ct)
                         ?? throw new AppException("ReportNotFound", "Không tìm thấy báo cáo.", 404);
            var response = Map(entity);
            await AttachTargetDetailsAsync(response, entity.target_type, entity.target_id, ct);
            return response;
        }

        public async Task<ReportResponse> UpdateStatusAsync(Guid moderatorAccountId, Guid reportId, ReportModerationUpdateRequest request, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(request.Status);
            var report = await _reportRepository.GetByIdAsync(reportId, ct)
                         ?? throw new AppException("ReportNotFound", "Không tìm thấy báo cáo.", 404);

            var previousStatus = report.status;
            report.status = normalizedStatus;
            report.moderator_id = moderatorAccountId;
            report.reviewed_at = TimezoneConverter.VietnamNow;

            await _reportRepository.UpdateAsync(report, ct);
            var saved = await _reportRepository.GetByIdAsync(report.report_id, ct)
                        ?? throw new InvalidOperationException("Failed to load report after update.");

            var transitionedFromPending = string.Equals(previousStatus, ReportStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                                          && !string.Equals(normalizedStatus, ReportStatuses.Pending, StringComparison.OrdinalIgnoreCase);

            if (normalizedStatus == ReportStatuses.Resolved &&
                !string.Equals(previousStatus, ReportStatuses.Resolved, StringComparison.OrdinalIgnoreCase))
            {
                await ApplyStrikePenaltyAsync(saved, ct);
            }

            if (transitionedFromPending)
            {
                await _contentModRepository.IncrementReportHandledAsync(moderatorAccountId, ct);
            }

            var response = Map(saved);
            await AttachTargetDetailsAsync(response, saved.target_type, saved.target_id, ct);
            return response;
        }

        public async Task<PagedResult<ReportResponse>> GetMyReportsAsync(Guid reporterAccountId, int page, int pageSize, CancellationToken ct = default)
        {
            var normalizedPage = NormalizePage(page);
            var normalizedSize = NormalizePageSize(pageSize);
            var (items, total) = await _reportRepository.GetByReporterAsync(reporterAccountId, normalizedPage, normalizedSize, ct);
            var responses = items.Select(Map).ToArray();
            foreach (var response in responses)
            {
                await AttachTargetDetailsAsync(response, response.TargetType, response.TargetId, ct);
            }

            return new PagedResult<ReportResponse>
            {
                Items = responses,
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        public async Task<ReportResponse> GetMyReportAsync(Guid reporterAccountId, Guid reportId, CancellationToken ct = default)
        {
            var entity = await _reportRepository.GetByIdAsync(reportId, ct)
                         ?? throw new AppException("ReportNotFound", "Không tìm thấy báo cáo.", 404);

            if (entity.reporter_id != reporterAccountId)
            {
                throw new AppException("ReportNotFound", "Không tìm thấy báo cáo.", 404);
            }

            var response = Map(entity);
            await AttachTargetDetailsAsync(response, entity.target_type, entity.target_id, ct);
            return response;
        }


        private async Task<TargetContext> GetTargetContextAsync(string targetType, Guid targetId, CancellationToken ct)
        {
            switch (targetType)
            {
                case ReportTargetTypes.Story:
                    {
                        var story = await _moderationRepository.GetStoryAsync(targetId, ct)
                                    ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);
                        return new TargetContext { Story = story };
                    }
                case ReportTargetTypes.Chapter:
                    {
                        var chapter = await _moderationRepository.GetChapterAsync(targetId, ct)
                                      ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);
                        return new TargetContext { Chapter = chapter };
                    }
                case ReportTargetTypes.Comment:
                    {
                        var comment = await _moderationRepository.GetCommentAsync(targetId, ct)
                                      ?? throw new AppException("CommentNotFound", "Không tìm thấy bình luận.", 404);
                        return new TargetContext { Comment = comment };
                    }
                default:
                    throw new AppException("UnsupportedTarget", $"Loại mục tiêu '{targetType}' không được hỗ trợ.", 400);
            }
        }

        private async Task<account> ResolveTargetAccountAsync(report report, CancellationToken ct)
        {
            var context = await GetTargetContextAsync(report.target_type, report.target_id, ct);
            var owner = context.TargetAccount;
            if (owner == null)
            {
                throw new AppException("TargetOwnerNotFound", "Không tìm thấy chủ sở hữu nội dung báo cáo.", 404);
            }

            var account = await _profileRepository.GetAccountByIdAsync(owner.account_id, ct)
                          ?? throw new AppException("TargetOwnerNotFound", "Không tìm thấy tài khoản của chủ sở hữu nội dung báo cáo.", 404);
            return account;
        }

        private async Task ApplyStrikePenaltyAsync(report report, CancellationToken ct)
        {
            var account = await ResolveTargetAccountAsync(report, ct);

            var readableReason = FormatReasonForDisplay(report.reason);
            var newStrike = (byte)Math.Min(3, account.strike + 1);
            var strikeStatus = account.strike_status ?? ReportStrikeStatus.None;
            DateTime? restrictedUntil = account.strike_restricted_until;

            if (newStrike >= 3)
            {
                strikeStatus = ReportStrikeStatus.Restricted;
                restrictedUntil = TimezoneConverter.VietnamNow.AddDays(7);
                newStrike = 3;
            }

            await _profileRepository.UpdateStrikeAsync(account.account_id, newStrike, strikeStatus, restrictedUntil, ct);

            await _mailSender.SendStrikeWarningEmailAsync(account.email, account.username, readableReason, newStrike, restrictedUntil);

            var message = BuildStrikeNotificationMessage(readableReason, newStrike, strikeStatus, restrictedUntil);
            await _notificationService.CreateAsync(new NotificationCreateModel(
                account.account_id,
                NotificationTypes.StrikeWarning,
                "Cảnh báo vi phạm",
                message,
                new
                {
                    reportId = report.report_id,
                    reason = readableReason,
                    strike = newStrike,
                    restrictedUntil
                }), ct);
        }

        private static string BuildStrikeNotificationMessage(string reason, byte strike, string strikeStatus, DateTime? restrictedUntil)
        {
            var message = $"Nội dung của bạn vi phạm quy định ({reason}). Strike hiện tại: {strike}.";
            if (string.Equals(strikeStatus, ReportStrikeStatus.Restricted, StringComparison.OrdinalIgnoreCase) && restrictedUntil.HasValue)
            {
                message += $" Tài khoản bị hạn chế đăng bài đến {restrictedUntil.Value:dd/MM/yyyy HH:mm}.";
            }

            return message;
        }

        private static class ReportStrikeStatus
        {
            public const string None = "none";
            public const string Restricted = "restricted";
        }

        private static string FormatReasonForDisplay(string reason)
        {
            return reason switch
            {
                ReportReasonCodes.NegativeContent => "Nội dung tiêu cực, gây thù ghét",
                ReportReasonCodes.Misinformation => "Truyền bá thông tin sai sự thật",
                ReportReasonCodes.Spam => "Spam",
                ReportReasonCodes.IntellectualProperty => "Vi phạm quyền sở hữu trí tuệ",
                _ => reason
            };
        }

        private sealed class TargetContext
        {
            public story? Story { get; init; }
            public chapter? Chapter { get; init; }
            public chapter_comment? Comment { get; init; }

            public account? TargetAccount =>
                Story?.author?.account
                ?? Chapter?.story?.author?.account
                ?? Comment?.reader?.account;
        }

        private static ReportResponse Map(report entity)
        {
            var reporter = entity.reporter ?? throw new InvalidOperationException("Reporter navigation not loaded.");
            var moderatorAccount = entity.moderator?.account;

            return new ReportResponse
            {
                ReportId = entity.report_id,
                TargetType = entity.target_type,
                TargetId = entity.target_id,
                ReporterId = entity.reporter_id,
                ReporterUsername = reporter.username,
                ModeratorId = entity.moderator_id,
                ModeratorUsername = moderatorAccount?.username,
                Reason = entity.reason,
                Details = entity.details,
                Status = entity.status,
                CreatedAt = entity.created_at,
                ReviewedAt = entity.reviewed_at
            };
        }

        private async Task AttachTargetDetailsAsync(ReportResponse response, string targetType, Guid targetId, CancellationToken ct)
        {
            switch (targetType)
            {
                case ReportTargetTypes.Story:
                    {
                        var story = await _moderationRepository.GetStoryAsync(targetId, ct);
                        if (story != null)
                        {
                            response.TargetAccountId = story.author_id;
                            response.Story = new ReportStoryDetailResponse
                            {
                                StoryId = story.story_id,
                                Title = story.title,
                                Description = story.desc,
                                Status = story.status,
                                CoverUrl = story.cover_url,
                                LengthPlan = story.length_plan,
                                AuthorId = story.author_id,
                                AuthorUsername = story.author?.account?.username ?? string.Empty
                            };
                        }
                        break;
                    }
                case ReportTargetTypes.Chapter:
                    {
                        var chapter = await _moderationRepository.GetChapterAsync(targetId, ct);
                        if (chapter != null)
                        {
                            response.TargetAccountId = chapter.story?.author_id ?? chapter.story?.author?.account_id;
                            response.Chapter = new ReportChapterDetailResponse
                            {
                                ChapterId = chapter.chapter_id,
                                StoryId = chapter.story_id,
                                Title = chapter.title,
                                ChapterNo = (int)chapter.chapter_no,
                                Status = chapter.status ?? string.Empty,
                                AccessType = chapter.access_type ?? string.Empty,
                                PriceDias = chapter.dias_price,
                                ContentPath = chapter.content_url,
                                LanguageCode = chapter.language?.lang_code,
                                LanguageName = chapter.language?.lang_name
                            };
                        }
                        break;
                    }
                case ReportTargetTypes.Comment:
                    {
                        var comment = await _moderationRepository.GetCommentAsync(targetId, ct);
                        if (comment != null)
                        {
                            response.TargetAccountId = comment.reader_id;
                            response.Comment = new ReportCommentDetailResponse
                            {
                                CommentId = comment.comment_id,
                                ChapterId = comment.chapter_id,
                                StoryId = comment.chapter?.story_id,
                                Content = comment.content,
                                Status = comment.status ?? string.Empty,
                                ReaderId = comment.reader_id,
                                ReaderUsername = comment.reader?.account?.username ?? string.Empty,
                                CreatedAt = comment.created_at
                            };
                        }
                        break;
                    }
            }
        }

        private static string NormalizeTargetType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidTargetType", "Loại mục tiêu là bắt buộc.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!ReportTargetTypes.Allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidTargetType", $"Loại mục tiêu '{value}' không được hỗ trợ.", 400);
            }

            return normalized;
        }

        private static string? NormalizeTargetTypeFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }
            return ReportTargetTypes.Allowed.Contains(value.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
                ? value.Trim().ToLowerInvariant()
                : throw new AppException("InvalidTargetType", $"Loại mục tiêu '{value}' không được hỗ trợ.", 400);
        }

        private static string NormalizeReason(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidReason", "Lý do là bắt buộc.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!ReportReasonCodes.Allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidReason", $"Lý do không hợp lệ '{value}'.", 400);
            }

            return normalized;
        }

        private static string NormalizeStatus(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new AppException("InvalidStatus", "Trạng thái là bắt buộc.", 400);
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!ReportStatuses.Allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Trạng thái không hợp lệ '{value}'.", 400);
            }

            return normalized;
        }

        private static string? NormalizeStatusFilter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (!ReportStatuses.Allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Trạng thái không hợp lệ '{value}'.", 400);
            }
            return normalized;
        }

        private static Guid? NormalizeGuidFilter(Guid? value)
        {
            if (!value.HasValue || value == Guid.Empty)
            {
                return null;
            }

            return value;
        }

        private static int NormalizePage(int page) => page <= 0 ? 1 : page;

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0) return 20;
            return pageSize > MaxPageSize ? MaxPageSize : pageSize;
        }
    }
}
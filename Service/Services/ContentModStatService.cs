using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.Common;
using Repository.DataModels;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class ContentModStatService : IContentModStatService
    {
        private readonly IContentModStatRepository _repository;

        public ContentModStatService(IContentModStatRepository repository)
        {
            _repository = repository;
        }

        public async Task<StatSeriesResponse> GetStoryPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default)
        {
            var (period, from, to) = NormalizeQuery(query);
            var points = await _repository.GetPublishedStoriesAsync(from, to, period, ct);
            return BuildResponse(period, points);
        }

        public async Task<StatSeriesResponse> GetChapterPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default)
        {
            var (period, from, to) = NormalizeQuery(query);
            var points = await _repository.GetPublishedChaptersAsync(from, to, period, ct);
            return BuildResponse(period, points);
        }

        public async Task<StatSeriesResponse> GetStoryDecisionStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default)
        {
            var normalized = NormalizeStoryDecisionStatus(status);
            var (period, from, to) = NormalizeQuery(query);
            var points = await _repository.GetStoryDecisionStatsAsync(normalized, from, to, period, ct);
            return BuildResponse(period, points);
        }

        public async Task<StatSeriesResponse> GetReportStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default)
        {
            var normalized = NormalizeReportStatus(status);
            var (period, from, to) = NormalizeQuery(query);
            var points = await _repository.GetReportStatsAsync(normalized, from, to, period, ct);
            return BuildResponse(period, points);
        }

        public async Task<StatSeriesResponse> GetHandledReportStatsAsync(string? status, Guid moderatorAccountId, StatQueryRequest query, CancellationToken ct = default)
        {
            var normalized = NormalizeHandledReportStatus(status);
            var (period, from, to) = NormalizeQuery(query);
            var points = await _repository.GetHandledReportsAsync(moderatorAccountId, normalized, from, to, period, ct);
            return BuildResponse(period, points);
        }

        public async Task<FileExportResponse> ExportStoryPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default)
        {
            var data = await GetStoryPublishStatsAsync(query, ct);
            var content = Helpers.ExcelHelper.GenerateStatSeriesExcel("Story Publish Stats", data);

            return CreateFileResponse(content, "story_publish_stats");
        }

        public async Task<FileExportResponse> ExportChapterPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default)
        {
            var data = await GetChapterPublishStatsAsync(query, ct);
            var content = Helpers.ExcelHelper.GenerateStatSeriesExcel("Chapter Publish Stats", data);

            return CreateFileResponse(content, "chapter_publish_stats");
        }

        public async Task<FileExportResponse> ExportStoryDecisionStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default)
        {
            var data = await GetStoryDecisionStatsAsync(status, query, ct);
            var title = string.IsNullOrEmpty(status) ? "Story Decisions" : $"Story Decisions ({status})";
            var content = Helpers.ExcelHelper.GenerateStatSeriesExcel(title, data);

            return CreateFileResponse(content, $"story_decisions_{status ?? "all"}");
        }

        public async Task<FileExportResponse> ExportReportStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default)
        {
            var data = await GetReportStatsAsync(status, query, ct);
            var title = string.IsNullOrEmpty(status) ? "Report Stats" : $"Report Stats ({status})";
            var content = Helpers.ExcelHelper.GenerateStatSeriesExcel(title, data);

            return CreateFileResponse(content, $"report_stats_{status ?? "all"}");
        }

        public async Task<FileExportResponse> ExportHandledReportStatsAsync(string? status, Guid moderatorAccountId, StatQueryRequest query, CancellationToken ct = default)
        {
            var data = await GetHandledReportStatsAsync(status, moderatorAccountId, query, ct);
            var title = string.IsNullOrEmpty(status) ? "Handled Reports" : $"Handled Reports ({status})";
            var content = Helpers.ExcelHelper.GenerateStatSeriesExcel(title, data);

            return CreateFileResponse(content, $"handled_reports_{status ?? "all"}");
        }

        private static FileExportResponse CreateFileResponse(byte[] content, string filePrefix)
        {
            return new FileExportResponse
            {
                Content = content,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"{filePrefix}_{DateTime.UtcNow:yyyyMMddHHmm}.xlsx"
            };
        }

        private static (string period, DateTime from, DateTime to) NormalizeQuery(StatQueryRequest query)
        {
            var period = string.IsNullOrWhiteSpace(query.Period) ? "month" : query.Period.Trim().ToLowerInvariant();
            if (period != "day" && period != "week" && period != "month" && period != "year")
            {
                throw new AppException("ValidationFailed", "Kỳ hạn phải là day, week, month hoặc year.", 400);
            }

            var to = query.To ?? TimezoneConverter.VietnamNow;
            var from = query.From ?? period switch
            {
                "day" => to.AddDays(-30),
                "week" => to.AddDays(-84),
                "year" => to.AddYears(-5),
                _ => to.AddMonths(-12)
            };

            if (from > to)
            {
                throw new AppException("ValidationFailed", "Ngày bắt đầu phải sớm hơn ngày kết thúc.", 400);
            }

            return (period, from, to);
        }

        private static string NormalizeStoryDecisionStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return string.Empty;
            }

            var normalized = status.Trim().ToLowerInvariant();
            return normalized switch
            {
                "approved" => "approved",
                "rejected" => "rejected",
                "pending" => "pending",
                _ => throw new AppException("ValidationFailed", "Giá trị trạng thái không được hỗ trợ.", 400)
            };
        }

        private static string NormalizeReportStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return string.Empty;
            }

            var normalized = status.Trim().ToLowerInvariant();
            return normalized switch
            {
                "pending" => "pending",
                "resolved" => "resolved",
                "rejected" => "rejected",
                _ => throw new AppException("ValidationFailed", "Giá trị trạng thái không được hỗ trợ.", 400)
            };
        }

        private static string NormalizeHandledReportStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return string.Empty;
            }

            var normalized = status.Trim().ToLowerInvariant();
            return normalized switch
            {
                "approved" => "resolved",
                "rejected" => "rejected",
                _ => throw new AppException("ValidationFailed", "Giá trị trạng thái không được hỗ trợ.", 400)
            };
        }

        private static StatSeriesResponse BuildResponse(string period, System.Collections.Generic.List<StatPointData> points)
        {
            var mapped = points.Select(p => new StatPointResponse
            {
                PeriodLabel = p.Label,
                PeriodStart = p.RangeStart.ToString("yyyy-MM-dd"),
                PeriodEnd = p.RangeEnd.ToString("yyyy-MM-dd"),
                Value = p.Value
            }).ToList();

            return new StatSeriesResponse
            {
                Period = period,
                Total = mapped.Sum(m => m.Value),
                Points = mapped
            };
        }
    }
}
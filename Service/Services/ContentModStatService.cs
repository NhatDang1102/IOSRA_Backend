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

        private static (string period, DateTime from, DateTime to) NormalizeQuery(StatQueryRequest query)
        {
            var period = string.IsNullOrWhiteSpace(query.Period) ? "month" : query.Period.Trim().ToLowerInvariant();
            if (period != "day" && period != "week" && period != "month" && period != "year")
            {
                throw new AppException("ValidationFailed", "Period must be day, week, month, or year.", 400);
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
                throw new AppException("ValidationFailed", "From date must be earlier than To date.", 400);
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
                _ => throw new AppException("ValidationFailed", "Unsupported status value.", 400)
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
                _ => throw new AppException("ValidationFailed", "Unsupported status value.", 400)
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
                _ => throw new AppException("ValidationFailed", "Unsupported status value.", 400)
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

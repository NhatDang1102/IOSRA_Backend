using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.OperationMod;
using Repository.DataModels;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Services
{
    public class OperationModStatService : IOperationModStatService
    {
        private readonly IOperationModStatRepository _repository;

        public OperationModStatService(IOperationModStatRepository repository)
        {
            _repository = repository;
        }

        public async Task<OperationRevenueResponse> GetRevenueStatsAsync(StatQueryRequest query, CancellationToken ct = default)
        {
            var (period, from, to) = NormalizeQuery(query);
            var data = await _repository.GetRevenueAsync(from, to, period, ct);
            return new OperationRevenueResponse
            {
                Period = period,
                DiaTopup = data.DiaTopup,
                Subscription = data.Subscription,
                VoiceTopup = data.VoiceTopup,
                Points = data.Points.Select(MapPoint).ToList()
            };
        }

        public async Task<OperationRequestStatResponse> GetRequestStatsAsync(string requestType, StatQueryRequest query, CancellationToken ct = default)
        {
            var normalized = NormalizeRequestType(requestType);
            var (period, from, to) = NormalizeQuery(query);
            var points = await _repository.GetRequestStatsAsync(normalized, from, to, period, ct);

            return new OperationRequestStatResponse
            {
                Type = normalized,
                Period = period,
                Total = points.Sum(p => p.Value),
                Points = points.Select(MapPoint).ToList()
            };
        }

        public async Task<OperationAuthorRevenueResponse> GetAuthorRevenueStatsAsync(string metric, StatQueryRequest query, CancellationToken ct = default)
        {
            var normalized = NormalizeMetric(metric);
            var (period, from, to) = NormalizeQuery(query);
            var points = await _repository.GetAuthorRevenueStatsAsync(normalized, from, to, period, ct);

            return new OperationAuthorRevenueResponse
            {
                Metric = normalized,
                Period = period,
                Total = points.Sum(p => p.Value),
                Points = points.Select(MapPoint).ToList()
            };
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

        private static string NormalizeRequestType(string requestType)
        {
            var normalized = requestType?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "rank_up" => "rank_up",
                "become_author" => "become_author",
                "withdraw" => "withdraw",
                _ => throw new AppException("ValidationFailed", "Unsupported request type.", 400)
            };
        }

        private static string NormalizeMetric(string metric)
        {
            var normalized = metric?.Trim().ToLowerInvariant();
            return normalized switch
            {
                "earned" => "earned",
                "withdrawn" => "withdrawn",
                _ => throw new AppException("ValidationFailed", "Metric must be earned or withdrawn.", 400)
            };
        }

        private static StatPointResponse MapPoint(StatPointData data)
            => new()
            {
                PeriodLabel = data.Label,
                PeriodStart = data.RangeStart.ToString("yyyy-MM-dd"),
                PeriodEnd = data.RangeEnd.ToString("yyyy-MM-dd"),
                Value = data.Value
            };
    }
}

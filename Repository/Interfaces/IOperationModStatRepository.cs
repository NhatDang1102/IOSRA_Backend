using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.DataModels;

namespace Repository.Interfaces
{
    public interface IOperationModStatRepository
    {
        Task<OperationRevenueData> GetRevenueAsync(DateTime from, DateTime to, string period, CancellationToken ct = default);
        Task<List<StatPointData>> GetRequestStatsAsync(string requestType, DateTime from, DateTime to, string period, CancellationToken ct = default);
        Task<List<StatPointData>> GetAuthorRevenueStatsAsync(string metric, DateTime from, DateTime to, string period, CancellationToken ct = default);
        Task IncrementReportGeneratedCountAsync(Guid userId, CancellationToken ct = default);
    }

    public class OperationRevenueData
    {
        public long DiaTopup { get; set; }
        public long Subscription { get; set; }
        public long VoiceTopup { get; set; }
        public List<StatPointData> Points { get; set; } = new();
    }
}

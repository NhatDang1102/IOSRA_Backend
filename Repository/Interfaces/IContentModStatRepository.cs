using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Models;

namespace Repository.Interfaces
{
    public interface IContentModStatRepository
    {
        Task<List<StatPointData>> GetPublishedStoriesAsync(DateTime from, DateTime to, string period, CancellationToken ct = default);
        Task<List<StatPointData>> GetPublishedChaptersAsync(DateTime from, DateTime to, string period, CancellationToken ct = default);
        Task<List<StatPointData>> GetStoryDecisionStatsAsync(string status, DateTime from, DateTime to, string period, CancellationToken ct = default);
        Task<List<StatPointData>> GetReportStatsAsync(string status, DateTime from, DateTime to, string period, CancellationToken ct = default);
        Task<List<StatPointData>> GetHandledReportsAsync(Guid moderatorAccountId, string status, DateTime from, DateTime to, string period, CancellationToken ct = default);
    }
}

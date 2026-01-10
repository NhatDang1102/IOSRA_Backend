using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.OperationMod;

using System.Collections.Generic;

namespace Service.Interfaces
{
    public interface IOperationModStatService
    {
        Task<OperationRevenueResponse> GetRevenueStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<OperationRequestStatResponse> GetRequestStatsAsync(string requestType, StatQueryRequest query, CancellationToken ct = default);
        Task<OperationAuthorRevenueResponse> GetAuthorRevenueStatsAsync(string metric, StatQueryRequest query, CancellationToken ct = default);

        Task<UserGrowthStatsResponse> GetUserGrowthStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<List<TrendingStoryResponse>> GetTrendingStoriesStatsAsync(StatQueryRequest query, int limit = 10, CancellationToken ct = default);
        Task<SystemEngagementResponse> GetSystemEngagementStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<List<TagTrendResponse>> GetTagTrendsStatsAsync(StatQueryRequest query, int limit = 10, CancellationToken ct = default);

        Task<FileExportResponse> ExportRevenueStatsAsync(StatQueryRequest query, Guid userId, CancellationToken ct = default);
        Task<FileExportResponse> ExportRequestStatsAsync(string requestType, StatQueryRequest query, Guid userId, CancellationToken ct = default);
        Task<FileExportResponse> ExportAuthorRevenueStatsAsync(string metric, StatQueryRequest query, Guid userId, CancellationToken ct = default);
    }
}

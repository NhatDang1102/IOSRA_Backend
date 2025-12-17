using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.OperationMod;

namespace Service.Interfaces
{
    public interface IOperationModStatService
    {
        Task<OperationRevenueResponse> GetRevenueStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<OperationRequestStatResponse> GetRequestStatsAsync(string requestType, StatQueryRequest query, CancellationToken ct = default);
        Task<OperationAuthorRevenueResponse> GetAuthorRevenueStatsAsync(string metric, StatQueryRequest query, CancellationToken ct = default);

        Task<FileExportResponse> ExportRevenueStatsAsync(StatQueryRequest query, Guid userId, CancellationToken ct = default);
        Task<FileExportResponse> ExportRequestStatsAsync(string requestType, StatQueryRequest query, Guid userId, CancellationToken ct = default);
        Task<FileExportResponse> ExportAuthorRevenueStatsAsync(string metric, StatQueryRequest query, Guid userId, CancellationToken ct = default);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.Common;

namespace Service.Interfaces
{
    public interface IContentModStatService
    {
        Task<StatSeriesResponse> GetStoryPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<StatSeriesResponse> GetChapterPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<StatSeriesResponse> GetStoryDecisionStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default);
        Task<StatSeriesResponse> GetReportStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default);
        Task<StatSeriesResponse> GetHandledReportStatsAsync(string? status, Guid moderatorAccountId, StatQueryRequest query, CancellationToken ct = default);

        Task<FileExportResponse> ExportStoryPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<FileExportResponse> ExportChapterPublishStatsAsync(StatQueryRequest query, CancellationToken ct = default);
        Task<FileExportResponse> ExportStoryDecisionStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default);
        Task<FileExportResponse> ExportReportStatsAsync(string? status, StatQueryRequest query, CancellationToken ct = default);
        Task<FileExportResponse> ExportHandledReportStatsAsync(string? status, Guid moderatorAccountId, StatQueryRequest query, CancellationToken ct = default);
    }
}

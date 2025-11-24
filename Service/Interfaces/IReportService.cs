using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Report;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Report;

namespace Service.Interfaces
{
    public interface IReportService
    {
        Task<ReportResponse> CreateAsync(Guid reporterAccountId, ReportCreateRequest request, CancellationToken ct = default);
        Task<PagedResult<ReportResponse>> ListAsync(string? status, string? targetType, Guid? targetId, int page, int pageSize, CancellationToken ct = default);
        Task<ReportResponse> GetAsync(Guid reportId, CancellationToken ct = default);
        Task<ReportResponse> UpdateStatusAsync(Guid moderatorAccountId, Guid reportId, ReportModerationUpdateRequest request, CancellationToken ct = default);
        Task<PagedResult<ReportResponse>> GetMyReportsAsync(Guid reporterAccountId, int page, int pageSize, CancellationToken ct = default);
        Task<ReportResponse> GetMyReportAsync(Guid reporterAccountId, Guid reportId, CancellationToken ct = default);
    }
}

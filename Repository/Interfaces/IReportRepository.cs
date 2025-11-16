using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IReportRepository
    {
        Task AddAsync(report entity, CancellationToken ct = default);
        Task UpdateAsync(report entity, CancellationToken ct = default);
        Task<report?> GetByIdAsync(Guid reportId, CancellationToken ct = default);
        Task<(List<report> Items, int Total)> GetPagedAsync(string? status, string? targetType, Guid? targetId, int page, int pageSize, CancellationToken ct = default);
        Task<bool> HasPendingReportAsync(Guid reporterId, string targetType, Guid targetId, CancellationToken ct = default);
        Task<(List<report> Items, int Total)> GetByReporterAsync(Guid reporterId, int page, int pageSize, CancellationToken ct = default);
    }
}

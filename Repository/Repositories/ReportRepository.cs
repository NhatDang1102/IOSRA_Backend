using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;

namespace Repository.Repositories
{
    public class ReportRepository : BaseRepository, IReportRepository
    {
        public ReportRepository(AppDbContext db) : base(db)
        {
        }

        public async Task AddAsync(report entity, CancellationToken ct = default)
        {
            EnsureId(entity, nameof(report.report_id));
            _db.report.Add(entity);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(report entity, CancellationToken ct = default)
        {
            _db.report.Update(entity);
            await _db.SaveChangesAsync(ct);
        }

        public Task<report?> GetByIdAsync(Guid reportId, CancellationToken ct = default)
            => _db.report
                  .AsNoTracking()
                  .Include(r => r.reporter)
                  .Include(r => r.moderator).ThenInclude(m => m.account)
                  .FirstOrDefaultAsync(r => r.report_id == reportId, ct);

        public async Task<(List<report> Items, int Total)> GetPagedAsync(string? status, string? targetType, Guid? targetId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _db.report
                .AsNoTracking()
                .Include(r => r.reporter)
                .Include(r => r.moderator).ThenInclude(m => m.account)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.status == status);
            }

            if (!string.IsNullOrWhiteSpace(targetType))
            {
                query = query.Where(r => r.target_type == targetType);
            }

            if (targetId.HasValue && targetId.Value != Guid.Empty)
            {
                query = query.Where(r => r.target_id == targetId.Value);
            }

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(r => r.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public Task<bool> HasPendingReportAsync(Guid reporterId, string targetType, Guid targetId, CancellationToken ct = default)
            => _db.report
                  .AsNoTracking()
                  .AnyAsync(r => r.reporter_id == reporterId
                                 && r.target_type == targetType
                                 && r.target_id == targetId
                                 && r.status == "pending", ct);

        public async Task<(List<report> Items, int Total)> GetByReporterAsync(Guid reporterId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = 20;

            var query = _db.report
                .AsNoTracking()
                .Include(r => r.reporter)
                .Where(r => r.reporter_id == reporterId)
                .OrderByDescending(r => r.created_at);

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }
    }
}

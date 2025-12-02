using System;
using System.Threading;
using System.Threading.Tasks;

namespace Repository.Interfaces
{
    public interface IContentModRepository
    {
        Task IncrementStoryDecisionAsync(Guid moderatorAccountId, bool approved, CancellationToken ct = default);
        Task IncrementChapterDecisionAsync(Guid moderatorAccountId, bool approved, CancellationToken ct = default);
        Task IncrementReportHandledAsync(Guid moderatorAccountId, CancellationToken ct = default);
    }
}

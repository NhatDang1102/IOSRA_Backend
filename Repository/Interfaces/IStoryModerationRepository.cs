using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;
using System;

namespace Repository.Interfaces
{
    public interface IStoryModerationRepository
    {
        Task<List<story>> GetStoriesForModerationAsync(IEnumerable<string> statuses, CancellationToken ct = default);
        Task<content_approve?> GetContentApprovalByIdAsync(Guid reviewId, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}


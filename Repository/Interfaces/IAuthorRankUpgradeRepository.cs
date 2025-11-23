using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IAuthorRankUpgradeRepository
    {
        Task<author?> GetAuthorAsync(Guid authorId, CancellationToken ct = default);
        Task<bool> HasPublishedStoryAsync(Guid authorId, CancellationToken ct = default);
        Task<bool> HasPendingRequestAsync(Guid authorId, CancellationToken ct = default);
        Task<author_rank_upgrade_request> CreateAsync(author_rank_upgrade_request entity, CancellationToken ct = default);
        Task<IReadOnlyList<author_rank_upgrade_request>> GetRequestsByAuthorAsync(Guid authorId, CancellationToken ct = default);
        Task<IReadOnlyList<author_rank_upgrade_request>> ListAsync(string? status, CancellationToken ct = default);
        Task<author_rank_upgrade_request?> GetByIdAsync(Guid requestId, CancellationToken ct = default);
        Task UpdateAsync(author_rank_upgrade_request entity, CancellationToken ct = default);
        Task<List<author_rank>> GetAllRanksAsync(CancellationToken ct = default);
        Task UpdateAuthorRankAsync(Guid authorId, Guid targetRankId, CancellationToken ct = default);
    }
}

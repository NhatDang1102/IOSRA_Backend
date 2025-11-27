using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IFavoriteStoryRepository
    {
        Task<favorite_story?> GetAsync(Guid readerId, Guid storyId, CancellationToken ct = default);
        Task AddAsync(favorite_story entity, CancellationToken ct = default);
        Task RemoveAsync(favorite_story entity, CancellationToken ct = default);
        Task<(IReadOnlyList<favorite_story> Items, int Total)> ListAsync(Guid readerId, int page, int pageSize, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
    }
}

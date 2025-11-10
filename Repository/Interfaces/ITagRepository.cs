using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface ITagRepository
    {
        Task<List<tag>> ListAsync(CancellationToken ct = default);
        Task<tag?> GetByIdAsync(Guid tagId, CancellationToken ct = default);
        Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
        Task<tag> CreateAsync(string name, CancellationToken ct = default);
        Task UpdateAsync(tag entity, CancellationToken ct = default);
        Task<bool> HasStoriesAsync(Guid tagId, CancellationToken ct = default);
        Task DeleteAsync(tag entity, CancellationToken ct = default);
        Task<List<tag>> GetTopAsync(int limit, CancellationToken ct = default);
        Task<List<tag>> ResolveAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
        Task<List<tag>> SearchAsync(string term, int limit, CancellationToken ct = default);
    }
}

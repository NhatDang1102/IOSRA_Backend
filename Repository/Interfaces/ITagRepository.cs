using Repository.Entities;

namespace Repository.Interfaces
{
    public interface ITagRepository
    {
        Task<List<tag>> ListAsync(CancellationToken ct = default);
        Task<tag?> GetByIdAsync(uint tagId, CancellationToken ct = default);
        Task<bool> ExistsByNameAsync(string name, uint? excludeId = null, CancellationToken ct = default);
        Task<tag> CreateAsync(string name, CancellationToken ct = default);
        Task UpdateAsync(tag entity, CancellationToken ct = default);
        Task<bool> HasStoriesAsync(uint tagId, CancellationToken ct = default);
        Task DeleteAsync(tag entity, CancellationToken ct = default);
    }
}

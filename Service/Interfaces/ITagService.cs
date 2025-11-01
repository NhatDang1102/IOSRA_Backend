using System;
using Contract.DTOs.Request.Tag;
using Contract.DTOs.Respond.Tag;

namespace Service.Interfaces
{
    public interface ITagService
    {
        Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken ct = default);
        Task<TagResponse> CreateAsync(TagCreateRequest req, CancellationToken ct = default);
        Task<TagResponse> UpdateAsync(Guid tagId, TagUpdateRequest req, CancellationToken ct = default);
        Task DeleteAsync(Guid tagId, CancellationToken ct = default);
    }
}

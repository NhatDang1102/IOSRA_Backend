using Contract.DTOs.Request.Tag;
using Contract.DTOs.Respond.Common;
using Contract.DTOs.Respond.Tag;
using System;

namespace Service.Interfaces
{
    public interface ITagService
    {
        Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken ct = default);
        Task<TagResponse> CreateAsync(TagCreateRequest req, CancellationToken ct = default);
        Task<TagResponse> UpdateAsync(Guid tagId, TagUpdateRequest req, CancellationToken ct = default);
        Task DeleteAsync(Guid tagId, CancellationToken ct = default);
        Task<List<TagOptionResponse>> GetTopOptionsAsync(int limit, CancellationToken ct = default);
        Task<List<TagOptionResponse>> ResolveOptionsAsync(TagResolveRequest request, CancellationToken ct = default);
        Task<List<TagOptionResponse>> GetOptionsAsync(string q, int limit, CancellationToken ct = default);
        Task<PagedResult<TagPagedItem>> GetPagedAsync(string? q, string sort, bool asc, int page, int pageSize, CancellationToken ct = default);
    }
}

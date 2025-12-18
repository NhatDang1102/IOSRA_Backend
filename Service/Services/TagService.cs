using System;
using Contract.DTOs.Request.Tag;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Tag;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;

namespace Service.Implementations
{
    public class TagService : ITagService
    {
        private readonly ITagRepository _tagRepo;

        private const int MaxNameLength = 64;

        public TagService(ITagRepository tagRepo)
        {
            _tagRepo = tagRepo;
        }

        public async Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken ct = default)
        {
            var tags = await _tagRepo.ListAsync(ct);
            return tags
                .Select(t => new TagResponse
                {
                    TagId = t.tag_id,
                    Name = t.tag_name
                })
                .ToList();
        }

        public async Task<TagResponse> CreateAsync(TagCreateRequest req, CancellationToken ct = default)
        {
            var name = NormalizeName(req.Name);
            ValidateName(name);

            if (await _tagRepo.ExistsByNameAsync(name, null, ct))
            {
                throw new AppException("TagAlreadyExists", "Thẻ đã tồn tại.", 409);
            }

            var entity = await _tagRepo.CreateAsync(name, ct);
            return new TagResponse
            {
                TagId = entity.tag_id,
                Name = entity.tag_name
            };
        }

        public async Task<TagResponse> UpdateAsync(Guid tagId, TagUpdateRequest req, CancellationToken ct = default)
        {
            var entity = await _tagRepo.GetByIdAsync(tagId, ct)
                         ?? throw new AppException("TagNotFound", "Không tìm thấy thẻ.", 404);

            var name = NormalizeName(req.Name);
            ValidateName(name);

            if (!string.Equals(entity.tag_name, name, StringComparison.Ordinal))
            {
                if (await _tagRepo.ExistsByNameAsync(name, tagId, ct))
                {
                    throw new AppException("TagAlreadyExists", "Thẻ đã tồn tại.", 409);
                }

                entity.tag_name = name;
                await _tagRepo.UpdateAsync(entity, ct);
            }

            return new TagResponse
            {
                TagId = entity.tag_id,
                Name = entity.tag_name
            };
        }

        public async Task DeleteAsync(Guid tagId, CancellationToken ct = default)
        {
            var entity = await _tagRepo.GetByIdAsync(tagId, ct)
                         ?? throw new AppException("TagNotFound", "Không tìm thấy thẻ.", 404);

            if (await _tagRepo.HasStoriesAsync(tagId, ct))
            {
                throw new AppException("TagInUse", "Không thể xóa thẻ vì đang được sử dụng.", 409);
            }

            await _tagRepo.DeleteAsync(entity, ct);
        }

        private static string NormalizeName(string? input)
        {
            return (input ?? string.Empty).Trim();
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new AppException("ValidationFailed", "Tên thẻ không được để trống.", 400);
            }

            if (name.Length > MaxNameLength)
            {
                throw new AppException("ValidationFailed", $"Tên thẻ không được vượt quá {MaxNameLength} ký tự.", 400);
            }
        }

        public async Task<List<TagOptionResponse>> GetTopOptionsAsync(int limit, CancellationToken ct = default)
        {
            var tags = await _tagRepo.GetTopAsync(limit, ct);
            return tags.Select(t => new TagOptionResponse
            {
                Value = t.tag_id,
                Label = t.tag_name
            }).ToList();
        }

        public async Task<List<TagOptionResponse>> ResolveOptionsAsync(TagResolveRequest request, CancellationToken ct = default)
        {
            if (request?.Ids == null || request.Ids.Count == 0) return new();
            var tags = await _tagRepo.ResolveAsync(request.Ids, ct);
            return tags.Select(t => new TagOptionResponse
            {
                Value = t.tag_id,
                Label = t.tag_name
            }).ToList();
        }

        public async Task<List<TagOptionResponse>> GetOptionsAsync(string q, int limit, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q)) return new();
            var rows = await _tagRepo.SearchAsync(q, limit, ct);
            return rows.Select(t => new TagOptionResponse
            {
                Value = t.tag_id,
                Label = t.tag_name
            }).ToList();
        }

        public async Task<PagedResult<TagPagedItem>> GetPagedAsync(string? q, string sort, bool asc, int page, int pageSize, CancellationToken ct = default)
        {
            var (items, total) = await _tagRepo.GetPagedAsync(q, sort, asc, page, pageSize, ct);

            var data = items.Select(x => new TagPagedItem
            {
                TagId = x.Tag.tag_id,
                Name = x.Tag.tag_name,
                Usage = x.Usage
            }).ToList();

            return new PagedResult<TagPagedItem>
            {
                Items = data,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}
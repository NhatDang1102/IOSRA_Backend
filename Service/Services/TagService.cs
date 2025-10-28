using Contract.DTOs.Request.Tag;
using Contract.DTOs.Respond.Tag;
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
                throw new AppException("TagExists", "tag da ton tai.", 409);
            }

            var entity = await _tagRepo.CreateAsync(name, ct);
            return new TagResponse
            {
                TagId = entity.tag_id,
                Name = entity.tag_name
            };
        }

        public async Task<TagResponse> UpdateAsync(uint tagId, TagUpdateRequest req, CancellationToken ct = default)
        {
            var entity = await _tagRepo.GetByIdAsync(tagId, ct)
                         ?? throw new AppException("NotFound", "tag khong ton tai.", 404);

            var name = NormalizeName(req.Name);
            ValidateName(name);

            if (!string.Equals(entity.tag_name, name, StringComparison.Ordinal))
            {
                if (await _tagRepo.ExistsByNameAsync(name, tagId, ct))
                {
                    throw new AppException("TagExists", "tag da ton tai.", 409);
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

        public async Task DeleteAsync(uint tagId, CancellationToken ct = default)
        {
            var entity = await _tagRepo.GetByIdAsync(tagId, ct)
                         ?? throw new AppException("NotFound", "tag khong ton tai.", 404);

            if (await _tagRepo.HasStoriesAsync(tagId, ct))
            {
                throw new AppException("TagInUse", "tag dang duoc su dung.", 409);
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
                throw new AppException("ValidationFailed", "ten tag khong duoc de trong.", 400);
            }

            if (name.Length > MaxNameLength)
            {
                throw new AppException("ValidationFailed", $"ten tag khong duoc dai hon {MaxNameLength} ky tu.", 400);
            }
        }
    }
}


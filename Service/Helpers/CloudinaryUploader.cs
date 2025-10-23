using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Contract.DTOs.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Repository.Entities;
using Service.Interfaces;

namespace Service.Helpers
{
    public class CloudinaryUploader : IImageUploader
    {
        private readonly Cloudinary _cloudinary;
        private readonly CloudinarySettings _settings;

        public CloudinaryUploader(IOptions<CloudinarySettings> options)
        {
            _settings = options.Value;
            var acc = new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret);
            _cloudinary = new Cloudinary(acc);
        }

        public async Task<string> UploadAvatarAsync(IFormFile file, string publicIdPrefix, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("File rỗng");

            await using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = string.IsNullOrWhiteSpace(_settings.Folder) ? "avatars" : _settings.Folder,
                PublicId = $"{publicIdPrefix}_{Guid.NewGuid():N}",
                Overwrite = true,
                UseFilename = false,
                UniqueFilename = true,
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };

            var result = await _cloudinary.UploadAsync(uploadParams, ct);
            if (result.StatusCode >= System.Net.HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(result.SecureUrl?.AbsoluteUri))
                throw new InvalidOperationException("Upload avatar thất bại");

            return result.SecureUrl.AbsoluteUri;
        }
    }
}

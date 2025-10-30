using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Contract.DTOs.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Service.Interfaces;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public class CloudinaryUploader : IImageUploader
    {
        private readonly Cloudinary _cloudinary;
        private readonly CloudinarySettings _settings;

        public CloudinaryUploader(IOptions<CloudinarySettings> options)
        {
            _settings = options.Value;
            var account = new Account(_settings.CloudName, _settings.ApiKey, _settings.ApiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadAvatarAsync(IFormFile file, string publicIdPrefix, CancellationToken ct = default)
        {
            if (file == null || file.Length == 0) throw new ArgumentException("File cannot be empty.");

            await using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = ResolveFolder("avatars"),
                PublicId = $"{publicIdPrefix}_{Guid.NewGuid():N}",
                Overwrite = true,
                UseFilename = false,
                UniqueFilename = true,
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };

            var result = await _cloudinary.UploadAsync(uploadParams, ct);
            if (result.StatusCode >= HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(result.SecureUrl?.AbsoluteUri))
                throw new InvalidOperationException("Failed to upload avatar image.");

            return result.SecureUrl.AbsoluteUri;
        }

        public async Task<string> UploadStoryCoverAsync(Stream imageStream, string fileName, CancellationToken ct = default)
        {
            if (imageStream == null || !imageStream.CanRead) throw new ArgumentException("Image stream is invalid.");

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, imageStream),
                Folder = ResolveFolder("story_covers"),
                Overwrite = true,
                UseFilename = false,
                UniqueFilename = true,
                Transformation = new Transformation().Quality("auto").FetchFormat("auto")
            };

            var result = await _cloudinary.UploadAsync(uploadParams, ct);
            if (result.StatusCode >= HttpStatusCode.BadRequest || string.IsNullOrWhiteSpace(result.SecureUrl?.AbsoluteUri))
                throw new InvalidOperationException("Failed to upload story cover image.");

            return result.SecureUrl.AbsoluteUri;
        }

        private string ResolveFolder(string defaultFolder)
        {
            if (string.IsNullOrWhiteSpace(_settings.Folder)) return defaultFolder;
            return $"{_settings.Folder.TrimEnd('/')}/{defaultFolder}";
        }
    }
}


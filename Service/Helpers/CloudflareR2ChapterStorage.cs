using System;
using Amazon.S3;
using Amazon.S3.Model;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public class CloudflareR2ChapterStorage : IChapterContentStorage
    {
        private readonly IAmazonS3 _s3Client;
        private readonly CloudflareR2Settings _settings;

        public CloudflareR2ChapterStorage(IAmazonS3 s3Client, IOptions<CloudflareR2Settings> options)
        {
            _s3Client = s3Client;
            _settings = options.Value;
        }

        public Task<string> UploadAsync(Guid storyId, Guid chapterId, string content, CancellationToken ct = default)
            => UploadInternalAsync(BuildObjectKey(storyId, chapterId), content, ct);

        public Task<string> UploadLocalizationAsync(Guid storyId, Guid chapterId, string languageCode, string content, CancellationToken ct = default)
        {
            var key = BuildLocalizationKey(storyId, chapterId, languageCode);
            return UploadInternalAsync(key, content, ct);
        }

        public async Task<string> DownloadAsync(string key, CancellationToken ct = default)
        {
            var request = new GetObjectRequest
            {
                BucketName = _settings.Bucket,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request, ct);
            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _settings.Bucket,
                Key = key
            };
            return _s3Client.DeleteObjectAsync(request, ct);
        }

        public string GetContentUrl(string key)
        {
            if (!string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            {
                return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key}";
            }

            return $"{_settings.Endpoint.TrimEnd('/')}/{_settings.Bucket}/{key}";
        }

        private async Task<string> UploadInternalAsync(string key, string content, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes, writable: false);
            stream.Position = 0;

            var request = new PutObjectRequest
            {
                BucketName = _settings.Bucket,
                Key = key,
                InputStream = stream,
                ContentType = "text/plain; charset=utf-8",
                AutoCloseStream = false,
                UseChunkEncoding = false
            };
            request.Headers.ContentLength = stream.Length;

            await _s3Client.PutObjectAsync(request, ct);
            return key;
        }

        private static string BuildObjectKey(Guid storyId, Guid chapterId)
            => $"stories/{storyId}/chapters/{chapterId}.txt";

        private static string BuildLocalizationKey(Guid storyId, Guid chapterId, string languageCode)
            => $"stories/{storyId}/chapters/{chapterId}/locales/{languageCode.ToLowerInvariant()}.txt";
    }
}

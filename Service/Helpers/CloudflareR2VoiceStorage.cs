using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;

namespace Service.Helpers
{
    public class CloudflareR2VoiceStorage : IVoiceAudioStorage
    {
        private readonly IAmazonS3 _s3Client;
        private readonly CloudflareR2Settings _settings;

        public CloudflareR2VoiceStorage(IAmazonS3 s3Client, IOptions<CloudflareR2Settings> options)
        {
            _s3Client = s3Client;
            _settings = options.Value;
        }

        public async Task<string> UploadAsync(Guid storyId, Guid chapterId, Guid voiceId, byte[] data, CancellationToken ct = default)
        {
            var key = BuildKey(storyId, chapterId, voiceId);

            using var stream = new MemoryStream(data);
            var request = new PutObjectRequest
            {
                BucketName = _settings.Bucket,
                Key = key,
                InputStream = stream,
                ContentType = "audio/mpeg",
                AutoCloseStream = false,
                UseChunkEncoding = false
            };
            request.Headers.ContentLength = stream.Length;

            await _s3Client.PutObjectAsync(request, ct);
            return key;
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

        public string GetPublicUrl(string key)
        {
            if (!string.IsNullOrWhiteSpace(_settings.PublicBaseUrl))
            {
                return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key}";
            }

            return $"{_settings.Endpoint.TrimEnd('/')}/{_settings.Bucket}/{key}";
        }

        private static string BuildKey(Guid storyId, Guid chapterId, Guid voiceId)
            => $"voices/{storyId}/{chapterId}/{voiceId}.mp3";
    }
}

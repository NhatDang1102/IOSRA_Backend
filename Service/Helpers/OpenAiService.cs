using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public class OpenAiService : IOpenAiModerationService, IOpenAiImageService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _settings;

        public OpenAiService(HttpClient httpClient, IOptions<OpenAiSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
        }

        public async Task<OpenAiModerationResult> ModerateStoryAsync(string title, string? description, CancellationToken ct = default)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.AppendLine(title);
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.AppendLine(description);
            }

            var payload = new ModerationRequest
            {
                Model = _settings.ModerationModel,
                Input = builder.Length > 0 ? builder.ToString() : "No content"
            };

            using var response = await _httpClient.PostAsJsonAsync("moderations", payload, cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI moderation failed with status {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<ModerationResponse>(cancellationToken: ct)
                         ?? throw new InvalidOperationException("OpenAI moderation returned an empty response.");

            var first = result.Results?.FirstOrDefault()
                        ?? throw new InvalidOperationException("OpenAI moderation did not return any results.");

            var categories = first.Categories?.Where(kv => kv.Value).Select(kv => kv.Key).Cast<string?>().ToArray() ?? Array.Empty<string?>();
            double? maxScore = null;
            if (first.CategoryScores != null && first.CategoryScores.Count > 0)
            {
                maxScore = first.CategoryScores.Values.Max();
            }

            return new OpenAiModerationResult(first.Flagged, maxScore, categories);
        }

        public async Task<OpenAiImageResult> GenerateCoverAsync(string prompt, CancellationToken ct = default)
        {
            var payload = new ImageRequest
            {
                Model = _settings.ImageModel,
                Prompt = prompt,
                Size = "1024x1024"
            };

            using var response = await _httpClient.PostAsJsonAsync("images/generations", payload, cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI image generation failed with status {(int)response.StatusCode}: {body}");
            }

            var result = await response.Content.ReadFromJsonAsync<ImageResponse>(cancellationToken: ct)
                         ?? throw new InvalidOperationException("OpenAI image generation returned an empty response.");

            var image = result.Data?.FirstOrDefault()
                        ?? throw new InvalidOperationException("OpenAI image generation did not return any images.");

            Stream stream;
            string fileName;
            string contentType;

            if (!string.IsNullOrWhiteSpace(image.Base64Data))
            {
                var bytes = Convert.FromBase64String(image.Base64Data);
                stream = new MemoryStream(bytes, writable: false);
                fileName = $"story_cover_{Guid.NewGuid():N}.png";
                contentType = "image/png";
            }
            else if (!string.IsNullOrWhiteSpace(image.Url))
            {
                using var downloadResponse = await _httpClient.GetAsync(image.Url, ct);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    var body = await downloadResponse.Content.ReadAsStringAsync(ct);
                    throw new InvalidOperationException($"Failed to download generated image: {(int)downloadResponse.StatusCode} {body}");
                }

                var memory = new MemoryStream();
                await downloadResponse.Content.CopyToAsync(memory, ct);
                memory.Position = 0;
                stream = memory;

                contentType = downloadResponse.Content.Headers.ContentType?.MediaType ?? "image/png";
                var extension = contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png";
                fileName = $"story_cover_{Guid.NewGuid():N}.{extension}";
            }
            else
            {
                throw new InvalidOperationException("OpenAI image generation returned an empty payload.");
            }

            stream.Position = 0;

            return new OpenAiImageResult(stream, fileName, contentType);
        }

        private sealed record ModerationRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; init; } = null!;

            [JsonPropertyName("input")]
            public string Input { get; init; } = null!;
        }

        private sealed record ModerationResponse
        {
            [JsonPropertyName("results")]
            public ModerationResult[]? Results { get; init; }
        }

        private sealed record ModerationResult
        {
            [JsonPropertyName("flagged")]
            public bool Flagged { get; init; }

            [JsonPropertyName("categories")]
            public Dictionary<string, bool>? Categories { get; init; }

            [JsonPropertyName("category_scores")]
            public Dictionary<string, double>? CategoryScores { get; init; }
        }

        private sealed record ImageRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; init; } = null!;

            [JsonPropertyName("prompt")]
            public string Prompt { get; init; } = null!;

            [JsonPropertyName("size")]
            public string Size { get; init; } = "1024x1024";
        }

        private sealed record ImageResponse
        {
            [JsonPropertyName("data")]
            public ImageData[]? Data { get; init; }
        }

        private sealed record ImageData
        {
            [JsonPropertyName("b64_json")]
            public string? Base64Data { get; init; }

            [JsonPropertyName("url")]
            public string? Url { get; init; }
        }
    }
}

using System;
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Helpers
{
    public class OpenAiService : IOpenAiModerationService, IOpenAiImageService
    {
        private static readonly RegexOptions MatchOptions = RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        private static readonly Regex WordRegex = new Regex(@"\b[\p{L}\p{Nd}_']+\b", MatchOptions);

        // Edit this list to control which words trigger deductions.
        private static readonly string[] BannedKeywords =
        {
            "fuck",
            "shit",
            "bullshit",
            "idiot",
            "stupid",
            "kill",
            "murder",
            "ngoc",
            "ngu"
        };

        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _settings;
        private readonly Dictionary<string, string> _normalizedToOriginal;
        private readonly JsonSerializerOptions _jsonOptions;

        public OpenAiService(HttpClient httpClient, IOptions<OpenAiSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;

            _normalizedToOriginal = BannedKeywords
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(keyword => (keyword, normalized: NormalizeToken(keyword)))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.normalized))
                .ToDictionary(entry => entry.normalized, entry => entry.keyword, StringComparer.OrdinalIgnoreCase);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<OpenAiModerationResult> ModerateStoryAsync(string title, string? description, CancellationToken ct = default)
        {
            var content = ComposeContent(title, description);
            var scan = ScanContent(content);

            var totalViolations = scan.Violations.Sum(v => v.Count);
            var score = Math.Max(0.0, 1.0 - totalViolations * 0.05);
            var shouldReject = score < 0.5;

            var explanation = await RequestExplanationAsync(
                title,
                description,
                scan.Violations,
                score,
                shouldReject,
                scan.Sanitized,
                ct);

            return new OpenAiModerationResult(
                shouldReject,
                score,
                scan.Violations,
                content,
                scan.Sanitized,
                explanation);
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

        private async Task<string> RequestExplanationAsync(
            string title,
            string? description,
            IReadOnlyList<ModerationViolation> violations,
            double score,
            bool shouldReject,
            string sanitizedContent,
            CancellationToken ct)
        {
            var keywordInstruction = $"You will subtract 0.05 for each following exacts word: {string.Join(", ", BannedKeywords)}.";

            var payload = new ChatCompletionsRequest
            {
                Model = _settings.ChatModel,
                Temperature = 0,
                MaxTokens = 500,
                Messages = new[]
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = "You are an assistant that explains automated content moderation decisions in English. Be concise, factual, and cite the detected words."
                    },
                    new ChatMessage
                    {
                        Role = "user",
                        Content = BuildModerationPrompt(keywordInstruction, title, description, violations, score, shouldReject, sanitizedContent)
                    }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                var failureBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI chat completion failed with status {(int)response.StatusCode}: {failureBody}");
            }

            var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct)
                          ?? throw new InvalidOperationException("OpenAI chat completion returned an empty response.");

            var content = envelope.Choices?
                               .Select(c => c.Message?.Content)
                               .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

            if (string.IsNullOrWhiteSpace(content))
            {
                return BuildFallbackExplanation(violations, score, shouldReject);
            }

            content = content.Trim();
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("explanation", out var explanationProp))
                {
                    var explanation = explanationProp.GetString();
                    if (!string.IsNullOrWhiteSpace(explanation))
                    {
                        return explanation.Trim();
                    }
                }
            }
            catch (JsonException)
            {
                // Ignore parse failure and fall back.
            }

            return BuildFallbackExplanation(violations, score, shouldReject);
        }

        private static string BuildModerationPrompt(
            string keywordInstruction,
            string title,
            string? description,
            IReadOnlyList<ModerationViolation> violations,
            double score,
            bool shouldReject,
            string sanitizedContent)
        {
            var summary = new
            {
                score = Math.Round(score, 2),
                shouldReject,
                violations = violations.Select(v => new
                {
                    word = v.Word,
                    count = v.Count,
                    samples = v.Samples
                })
            };

            var sb = new StringBuilder();
            sb.AppendLine(keywordInstruction);
            sb.AppendLine("Initial score is 1.00. Each occurrence of a listed word reduces the score by 0.05. The score cannot be lower than 0. The story is rejected if the final score is below 0.50.");
            sb.AppendLine("Story content:");
            sb.AppendLine($"Title: {(string.IsNullOrWhiteSpace(title) ? "(empty)" : title)}");
            sb.AppendLine($"Description: {(string.IsNullOrWhiteSpace(description) ? "(empty)" : description)}");
            sb.AppendLine("Summary produced by the rule:");
            sb.AppendLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            sb.AppendLine("Sanitized content (detected words masked with asterisks):");
            sb.AppendLine(string.IsNullOrWhiteSpace(sanitizedContent) ? "(empty)" : sanitizedContent);
            sb.AppendLine("Explain the decision to the author in English. Mention every detected word. If no words were detected, say that the content is clean.");
            sb.AppendLine("Respond ONLY in JSON with this shape:");
            sb.Append("{\"score\":0.95,\"shouldReject\":false,\"explanation\":\"Short English explanation\",\"violations\":[{\"word\":\"example\",\"count\":1,\"summary\":\"Describe why it was flagged.\"}]}");
            return sb.ToString();
        }

        private static string BuildFallbackExplanation(IReadOnlyList<ModerationViolation> violations, double score, bool shouldReject)
        {
            if (violations.Count == 0)
            {
                return $"No banned words were detected. Final score remains {score:0.00}.";
            }

            var words = violations.Select(v => $"{v.Word} (x{v.Count})");
            var summary = string.Join(", ", words);
            var decision = shouldReject ? "Content is rejected because the score dropped below 0.50." : "Content passes the automated moderation.";
            return $"Detected banned words: {summary}. Final score is {score:0.00}. {decision}";
        }

        private ScanResult ScanContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ScanResult(string.Empty, Array.Empty<ModerationViolation>());
            }

            var buffer = content.ToCharArray();
            var buckets = new Dictionary<string, KeywordAccumulator>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in WordRegex.Matches(content))
            {
                if (!match.Success || match.Length == 0)
                {
                    continue;
                }

                var normalizedToken = NormalizeToken(match.Value);
                if (string.IsNullOrWhiteSpace(normalizedToken))
                {
                    continue;
                }

                if (!_normalizedToOriginal.TryGetValue(normalizedToken, out var originalKeyword))
                {
                    continue;
                }

                if (!buckets.TryGetValue(originalKeyword, out var accumulator))
                {
                    accumulator = new KeywordAccumulator(originalKeyword);
                    buckets[originalKeyword] = accumulator;
                }

                accumulator.Count++;
                if (accumulator.Samples.Count < 3)
                {
                    accumulator.Samples.Add(GetExcerpt(content, match.Index, match.Length));
                }

                for (var i = match.Index; i < match.Index + match.Length && i < buffer.Length; i++)
                {
                    buffer[i] = '*';
                }
            }

            var violations = buckets
                .OrderByDescending(kvp => kvp.Value.Count)
                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => new ModerationViolation(
                    kvp.Key,
                    kvp.Value.Count,
                    kvp.Value.Samples.ToArray()))
                .ToArray();

            var sanitized = violations.Length == 0 ? content : new string(buffer);
            return new ScanResult(sanitized, violations);
        }

        private static string ComposeContent(string title, string? description)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.AppendLine(title.Trim());
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                builder.AppendLine(description.Trim());
            }

            return builder.Length > 0 ? builder.ToString().Trim() : string.Empty;
        }

        private static string NormalizeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var noDiacritics = RemoveDiacritics(value);
            return noDiacritics.ToLowerInvariant();
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string GetExcerpt(string content, int index, int length, int radius = 30)
        {
            var start = Math.Max(0, index - radius);
            var end = Math.Min(content.Length, index + length + radius);

            var excerpt = content.Substring(start, end - start);
            if (start > 0)
            {
                excerpt = "…" + excerpt;
            }

            if (end < content.Length)
            {
                excerpt += "…";
            }

            return excerpt;
        }

        private sealed record ScanResult(string Sanitized, IReadOnlyList<ModerationViolation> Violations);

        private sealed class KeywordAccumulator
        {
            public KeywordAccumulator(string word)
            {
                Word = word;
            }

            public string Word { get; }
            public int Count { get; set; }
            public HashSet<string> Samples { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed record ChatCompletionsRequest
        {
            [JsonPropertyName("model")]
            public string Model { get; init; } = null!;

            [JsonPropertyName("temperature")]
            public double Temperature { get; init; }

            [JsonPropertyName("max_tokens")]
            public int MaxTokens { get; init; }

            [JsonPropertyName("messages")]
            public ChatMessage[] Messages { get; init; } = Array.Empty<ChatMessage>();
        }

        private sealed record ChatMessage
        {
            [JsonPropertyName("role")]
            public string Role { get; init; } = null!;

            [JsonPropertyName("content")]
            public string Content { get; init; } = null!;
        }

        private sealed record ChatCompletionsResponse
        {
            [JsonPropertyName("choices")]
            public ChatChoice[]? Choices { get; init; }
        }

        private sealed record ChatChoice
        {
            [JsonPropertyName("message")]
            public ChatMessage? Message { get; init; }
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

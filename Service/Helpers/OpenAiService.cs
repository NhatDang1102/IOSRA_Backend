using System;
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
using Contract.DTOs.Settings;
using Microsoft.Extensions.Options;
using Service.Interfaces;

namespace Service.Helpers
{
    public class OpenAiService : IOpenAiModerationService, IOpenAiImageService, IOpenAiTranslationService
    {
        private const double AutoApproveThreshold = 7.0;
        private const double ManualReviewThreshold = 5.0;

        private static readonly string[] PolicyStatements =
        {
            "Detect explicit/pornographic sexual content (especially involving minors or coercion).",
            "Detect violent, gory, or extremist rhetoric that glorifies harm.",
            "Detect URLs, links, or attempts to redirect readers off the platform.",
            "Detect spam or nonsensical content (repeated characters/words, placeholder text).",
            "Detect hate speech or harassment toward protected classes or individuals.",
            "Detect self-harm or suicide promotion/instructions.",
            "Detect instructions or promotion of illegal activities (drugs, hacking, weapons, etc.).",
            "Detect sharing of personal data/doxxing (phone, address, bank info).",
            "Detect low-quality/irrelevant filler or advertising masquerading as a chapter."
        };

        private static readonly object[] DeductionTable =
        {
            new
            {
                category = "Explicit sexual content",
                labels = new[] { "sexual_explicit", "sexual_minor", "sexual_transaction", "fetish_extreme" },
                penalties = new[] { "-3.0 severe (graphic sex, minors, non-consensual)", "-1.5 moderate (nudity, heavy innuendo)", "-0.5 mild borderline romance" },
                note = "Any minors + sexual context must be labelled and usually rejected."
            },
            new
            {
                category = "Violent / extremist",
                labels = new[] { "violent_gore", "extremist_rhetoric" },
                penalties = new[] { "-3.0 graphic gore or propaganda", "-1.5 praising violence", "-0.5 contextual combat" },
                note = "Genocide/terror support should never be auto approved."
            },
            new
            {
                category = "URL / redirect",
                labels = new[] { "url_redirect" },
                penalties = new[] { "-1.5 per link or CTA to leave IOSRA" },
                note = "Plain brand mentions without link = 0."
            },
            new
            {
                category = "Spam / gibberish",
                labels = new[] { "spam_repetition" },
                penalties = new[] { "-1.5 heavy spam or nonsense", "-0.5 short bursts" },
                note = "Use when chapter lacks meaningful prose."
            },
            new
            {
                category = "Hate speech / harassment",
                labels = new[] { "hate_speech", "harassment_targeted", "mild_insult" },
                penalties = new[] { "-3.0 protected-class slurs", "-2.0 repeated harassment", "-0.5 mild insults" },
                note = "Protected class attacks should drop score below 5."
            },
            new
            {
                category = "Self-harm & suicide",
                labels = new[] { "self_harm_promotion", "self_harm_instruction", "self_harm_neutral" },
                penalties = new[] { "-3.0 promotion/instructions", "-1.0 neutral mention needing caution" },
                note = "Never glorify or teach self-harm."
            },
            new
            {
                category = "Illegal activities",
                labels = new[] { "illegal_instruction", "illegal_promotion" },
                penalties = new[] { "-2.5 actionable how-to guides", "-1.0 glorifying crimes", "-0.5 incidental mention" },
                note = "Differentiate narrative vs instructions."
            },
            new
            {
                category = "Personal data / doxxing",
                labels = new[] { "personal_data" },
                penalties = new[] { "-2.5 exposing private info or urging doxxing" },
                note = "Mask or paraphrase sensitive numbers."
            },
            new
            {
                category = "Low quality / irrelevant",
                labels = new[] { "low_quality", "irrelevant_ad" },
                penalties = new[] { "-1.5 advertisements/placeholder text", "-0.5 mild off-topic content" },
                note = "Apply to chapters with <100 useful words."
            }
        };

        private static readonly HashSet<string> ForceRejectLabels = new(
            new[]
            {
                "sexual_minor",
                "sexual_transaction",
                "sexual_degradation",
                "extremist_rhetoric",
                "self_harm_promotion",
                "self_harm_instruction"
            },
            StringComparer.OrdinalIgnoreCase);

        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _settings;
        private readonly JsonSerializerOptions _jsonOptions;

        private static readonly ModerationProfile StoryProfile = new(
            ContentType: "story",
            PrimaryLabel: "Title",
            SecondaryLabel: "Description");

        private static readonly ModerationProfile ChapterProfile = new(
            ContentType: "chapter",
            PrimaryLabel: "Title",
            SecondaryLabel: "Body");

        public OpenAiService(HttpClient httpClient, IOptions<OpenAiSettings> options)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public Task<OpenAiModerationResult> ModerateStoryAsync(string title, string? description, CancellationToken ct = default)
            => ModerateContentAsync(title, description, StoryProfile, ct);

        public Task<OpenAiModerationResult> ModerateChapterAsync(string title, string content, CancellationToken ct = default)
            => ModerateContentAsync(title, content, ChapterProfile, ct);

        private async Task<OpenAiModerationResult> ModerateContentAsync(string primary, string? secondary, ModerationProfile profile, CancellationToken ct)
        {
            var combined = ComposeContent(primary, secondary);
            var ai = await RequestModerationAsync(profile, combined, ct);

            var rawScore = ai?.Score ?? 10.0;
            var score = Math.Clamp(Math.Round(rawScore, 2, MidpointRounding.AwayFromZero), 0.0, 10.0);
            var normalizedDecision = ai?.Decision?.ToLowerInvariant();

            bool shouldReject = normalizedDecision switch
            {
                "rejected" => true,
                "auto_approved" => score < ManualReviewThreshold ? true : false,
                "pending_manual_review" => score < ManualReviewThreshold,
                _ => score < ManualReviewThreshold
            };

            if (normalizedDecision is null)
            {
                if (score >= AutoApproveThreshold)
                {
                    normalizedDecision = "auto_approved";
                }
                else if (score >= ManualReviewThreshold)
                {
                    normalizedDecision = "pending_manual_review";
                }
                else
                {
                    normalizedDecision = "rejected";
                }
            }

            var autoApproved = !shouldReject && score >= AutoApproveThreshold;

            var violations = ai?.Violations?
                .Select(v =>
                {
                    var label = string.IsNullOrWhiteSpace(v.Label) ? "violation" : v.Label;
                    var evidence = v.Evidence ?? Array.Empty<string>();
                    return new ModerationViolation(label, Math.Max(1, evidence.Length), evidence);
                })
                .ToArray() ?? Array.Empty<ModerationViolation>();

            if (!shouldReject && ai?.Violations != null)
            {
                var hasForced = ai.Violations.Any(v => v.Label != null && ForceRejectLabels.Contains(v.Label));
                if (hasForced)
                {
                    shouldReject = true;
                    normalizedDecision = "rejected";
                }
            }

            var explanation = BuildExplanationFromAi(profile, score, normalizedDecision, ai?.Explanation)
                              ?? BuildDecisionExplanation(profile, score, shouldReject, autoApproved);

            return new OpenAiModerationResult(
                shouldReject,
                score,
                violations,
                combined,
                combined,
                explanation);
        }

        private async Task<ModerationAiResponse?> RequestModerationAsync(ModerationProfile profile, string content, CancellationToken ct)
        {
            var moderationInstructions = @"Return JSON only with shape { ""score"": number, ""decision"": ""auto_approved|pending_manual_review|rejected"", ""violations"": [{ ""label"": string, ""evidence"": [string] }], ""explanation"": { ""english"": string, ""vietnamese"": string } }.
Start from base score = 10.00 and subtract penalties exactly as defined in ""deductions"". Every time you subtract points you MUST add a violation entry (label must match the table) and quote the offending snippet inside ""evidence"".
Rules that must always be enforced:
- Any URL, external link, or redirect CTA (http, https, www, .com, bit.ly, telegram, discord.gg, invite codes, etc.) => label ""url_redirect"" and subtract at least 1.5 points per link.
- Spam, nonsense, or repeated tokens (""up up up"", ""aaaaaaaa"", ""test test"", placeholder text) => label ""spam_repetition"" and subtract at least 1.0 point.
- Explicit sexual content, violence, hate speech, self-harm, illegal instructions, personal data, and irrelevant ads must follow the deduction table. Protected-class hate or sexual content with minors should reduce the score below 5 and typically be rejected.
- If ANY violation exists, the final score must be < 10 and the violation must be listed. Never return 10.00 when a deduction was applied.
- Decision mapping: score >= 7 and no forced rejection => auto_approved; 5 <= score < 7 => pending_manual_review; score < 5 or forced labels => rejected.
Explanation requirements:
- Always provide both English and Vietnamese summaries.
- Mention each deduction explicitly, e.g., ""-1.5 for url_redirect because the text contains http://example"".
- Paraphrase slurs rather than repeating them verbatim.
If no policy issue exists, state clearly that no deductions were applied.";

            var userPayload = new
            {
                contentType = profile.ContentType,
                scoring = new
                {
                    autoApprove = AutoApproveThreshold,
                    manualReview = ManualReviewThreshold
                },
                policies = PolicyStatements,
                deductions = DeductionTable,
                instructions = moderationInstructions,
                content
            };

            var payload = new ChatCompletionsRequest
            {
                Model = _settings.ChatModel,
                Temperature = 0,
                MaxTokens = 700,
                Messages = new[]
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = "You are an automated moderation engine. Start from score 10, subtract penalties exactly as defined, enforce every policy (URL/spam detection included), and respond ONLY with JSON in the requested schema."
                    },
                    new ChatMessage
                    {
                        Role = "user",
                        Content = JsonSerializer.Serialize(userPayload, _jsonOptions)
                    }
                }
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
                request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var failureBody = await response.Content.ReadAsStringAsync(ct);
                    throw new InvalidOperationException($"OpenAI moderation request failed with status {(int)response.StatusCode}: {failureBody}");
                }

                var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct)
                              ?? throw new InvalidOperationException("OpenAI moderation response was empty.");

                var contentResponse = envelope.Choices?
                    .Select(c => c.Message?.Content)
                    .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

                if (!string.IsNullOrWhiteSpace(contentResponse))
                {
                    return JsonSerializer.Deserialize<ModerationAiResponse>(contentResponse, _jsonOptions);
                }
            }
            catch
            {
                // fall through to default handling
            }

            return null;
        }

        private static string? BuildExplanationFromAi(ModerationProfile profile, double score, string? decision, ModerationAiExplanation? explanation)
        {
            var english = explanation?.English;
            var vietnamese = explanation?.Vietnamese;
            if (!string.IsNullOrWhiteSpace(english) && !string.IsNullOrWhiteSpace(vietnamese))
            {
                return $"English:\n{english.Trim()}\n\nTiáº¿ng Viá»‡t:\n{vietnamese.Trim()}";
            }

            var shouldReject = string.Equals(decision, "rejected", StringComparison.OrdinalIgnoreCase);
            var autoApproved = string.Equals(decision, "auto_approved", StringComparison.OrdinalIgnoreCase);
            return BuildDecisionExplanation(profile, score, shouldReject, autoApproved);
        }

        private static string BuildDecisionExplanation(ModerationProfile profile, double score, bool shouldReject, bool autoApproved)
        {
            var english = shouldReject
                ? $"Automated moderation scored {score:0.00}/10 after policy deductions (details unavailable), which is below {ManualReviewThreshold:0.00}, so this {profile.ContentType} was rejected."
                : autoApproved
                    ? $"Automated moderation scored {score:0.00}/10 after policy deductions (details unavailable), meeting the auto-approval threshold of {AutoApproveThreshold:0.00}, so the {profile.ContentType} was published."
                    : $"Automated moderation scored {score:0.00}/10 after policy deductions (details unavailable), which is below {AutoApproveThreshold:0.00} but at or above {ManualReviewThreshold:0.00}, so the {profile.ContentType} requires manual review.";

            var vietnamese = shouldReject
                   ? $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), thấp hơn {ManualReviewThreshold:0.00} nên nội dung {profile.ContentType} bị từ chối."
                : autoApproved
                         ? $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), đạt ngưỡng tự duyệt {AutoApproveThreshold:0.00} nên nội dung {profile.ContentType} được xuất bản."
                    : $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), thấp hơn {AutoApproveThreshold:0.00} nhưng không dưới {ManualReviewThreshold:0.00} nên nội dung {profile.ContentType} chuyển cho moderator.";

            return $"English:\n{english}\n\nTiáº¿ng Viá»‡t:\n{vietnamese}";
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

        public async Task<string> TranslateAsync(string content, string sourceLanguageCode, string targetLanguageCode, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException("Content must not be empty.", nameof(content));
            }

            if (string.IsNullOrWhiteSpace(sourceLanguageCode) || string.IsNullOrWhiteSpace(targetLanguageCode))
            {
                throw new ArgumentException("Language codes are required.");
            }

            var systemPrompt = $"You are a professional literary translator. Translate from {sourceLanguageCode} to {targetLanguageCode}. Preserve the author's tone, keep paragraph breaks, and return plain text only without commentary.";
            var payload = new ChatCompletionsRequest
            {
                Model = _settings.ChatModel,
                Temperature = 0.1,
                MaxTokens = 4096,
                Messages = new[]
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = content }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                var failureBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI translation failed with status {(int)response.StatusCode}: {failureBody}");
            }

            var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct)
                          ?? throw new InvalidOperationException("OpenAI translation returned an empty response.");

            var translated = envelope.Choices?
                .Select(c => c.Message?.Content)
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

            if (string.IsNullOrWhiteSpace(translated))
            {
                throw new InvalidOperationException("OpenAI translation response did not contain any content.");
            }

            return translated.Trim();
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

        private sealed record ModerationProfile(
            string ContentType,
            string PrimaryLabel,
            string SecondaryLabel);

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

        private sealed record ModerationAiResponse
        {
            [JsonPropertyName("score")]
            public double? Score { get; init; }

            [JsonPropertyName("decision")]
            public string? Decision { get; init; }

            [JsonPropertyName("violations")]
            public ModerationAiViolation[]? Violations { get; init; }

            [JsonPropertyName("explanation")]
            public ModerationAiExplanation? Explanation { get; init; }
        }

        private sealed record ModerationAiViolation
        {
            [JsonPropertyName("label")]
            public string? Label { get; init; }

            [JsonPropertyName("evidence")]
            public string[]? Evidence { get; init; }
        }

        private sealed record ModerationAiExplanation
        {
            [JsonPropertyName("english")]
            public string? English { get; init; }

            [JsonPropertyName("vietnamese")]
            public string? Vietnamese { get; init; }
        }
    }
}


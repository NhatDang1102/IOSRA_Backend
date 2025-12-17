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
    public class OpenAiService : IOpenAiModerationService, IOpenAiImageService, IOpenAiTranslationService, IOpenAiChatService
    {

        //define các ngưỡng điểm chấm AI 
        private const double AutoApproveThreshold = 7.0;
        private const double ManualReviewThreshold = 5.0;


        //define phạm vi kiểm duyệt để lọc 
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
            "Detect low-quality/irrelevant filler or advertising masquerading as a chapter.",
            "Detect frequent grammar, spelling, or punctuation errors.",
            "Detect poor formatting, excessive capitalization, or 'wall of text' paragraphs.",
            "Detect weak, bland, or excessively repetitive prose."
        };

        //từ những phạm vi trên, định ra mức trừ điểm (Ở MỨC TƯƠNG ĐỐI vì AI mỗi lần response khác nhau)
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
            },
            new
            {
                category = "Writing Quality",
                labels = new[] { "grammar_spelling", "poor_formatting", "weak_prose" },
                penalties = new[] { "-0.5 frequent typos/grammar errors", "-0.5 poor formatting (wall of text, capitalization)", "-0.25 basic/repetitive prose" },
                note = "Penalize amateur writing styles even if content is safe."
            }
        };

        //define các mood để tạo nhạc
        private static readonly string[] MoodCodes = { "calm", "sad", "mysterious", "excited", "romantic", "neutral" };


        //define các label nặng nhất (để reject luôn)
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


        //định ra những field bị kiểm duyệt (story thì kiểm title và description, chapter thì title và content)
        private static readonly ModerationProfile StoryProfile = new(
            ContentType: "story",
            PrimaryLabel: "Title",
            SecondaryLabel: "Description",
            TertiaryLabel: "Outline");

        private static readonly ModerationProfile ChapterProfile = new(
            ContentType: "chapter",
            PrimaryLabel: "Title",
            SecondaryLabel: "Body",
            TertiaryLabel: null);

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

        public Task<OpenAiModerationResult> ModerateStoryAsync(string title, string? description, string outline, CancellationToken ct = default)
            => ModerateContentAsync(title, description, outline, StoryProfile, ct);

        public Task<OpenAiModerationResult> ModerateChapterAsync(string title, string content, CancellationToken ct = default)
            => ModerateContentAsync(title, content, null, ChapterProfile, ct);


        //hàm quét AI kiểm duyệt 
        private async Task<OpenAiModerationResult> ModerateContentAsync(string primary, string? secondary, string? tertiary, ModerationProfile profile, CancellationToken ct)
        {

            //gắn hết content vô 1 chuỗi để request cho openai 
            var combined = ComposeContent(primary, secondary, tertiary);
            var ai = await RequestModerationAsync(profile, combined, ct);

            // Calculate score from penalties to ensure accuracy
            var totalDeduction = ai?.Violations?.Sum(v => Math.Abs(v.Penalty ?? 0)) ?? 0;
            var rawScore = 10.0 - totalDeduction;
            
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

        //hàm request gửi cho OpenAI để chấm điểm trc 
        private async Task<ModerationAiResponse?> RequestModerationAsync(ModerationProfile profile, string content, CancellationToken ct)
        {
            //define system openAI 
            var moderationInstructions = @"Return JSON only with shape { ""score"": number, ""decision"": ""auto_approved|pending_manual_review|rejected"", ""violations"": [{ ""label"": string, ""evidence"": [string], ""penalty"": number }], ""explanation"": { ""english"": string, ""vietnamese"": string } }.
Start from base score = 10.00 and subtract penalties exactly as defined in ""deductions"". Every time you subtract points you MUST add a violation entry (label must match the table), set the ""penalty"" field to the positive number of points deducted (e.g. 1.5), and quote the offending snippet inside ""evidence"".
Rules that must always be enforced:
- Any URL, external link, or redirect CTA (http, https, www, .com, bit.ly, telegram, discord.gg, invite codes, etc.) => label ""url_redirect"" and subtract at least 1.5 points per link.
- Spam, nonsense, or repeated tokens (""up up up"", ""aaaaaaaa"", ""test test"", placeholder text) => label ""spam_repetition"" and subtract at least 1.0 point.
- Explicit sexual content, violence, hate speech, self-harm, illegal instructions, personal data, and irrelevant ads must follow the deduction table. Protected-class hate or sexual content with minors should reduce the score below 5 and typically be rejected.
- If ANY violation exists, the violation must be listed with its penalty. Never return 10.00 when a deduction was applied.
- Decision mapping: score >= 7 and no forced rejection => auto_approved; 5 <= score < 7 => pending_manual_review; score < 5 or forced labels => rejected.
Explanation requirements:
- Always provide both English and Vietnamese summaries.
- Describe each violation and the penalty applied (e.g. ""-1.5 for url_redirect"").
- DO NOT state the final calculated score in the text summary; the system will display it based on the penalties.
- Paraphrase slurs rather than repeating them verbatim.
If no policy issue exists, state clearly that no deductions were applied.";

            //gom hết phạm vi luật, ngưỡng điểm, instruction vào 1 payload 
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


            //từ payload trên gửi cho AI chấm điểm (gọi api ở bước này)
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

                //throw lỗi nếu api openAI failed 
                if (!response.IsSuccessStatusCode)
                {
                    var failureBody = await response.Content.ReadAsStringAsync(ct);
                    throw new InvalidOperationException($"OpenAI moderation request failed with status {(int)response.StatusCode}: {failureBody}");
                }


                //check json response
                var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct)
                              ?? throw new InvalidOperationException("OpenAI moderation response was empty.");

                
                var contentResponse = envelope.Choices?
                    .Select(c => c.Message?.Content)
                    .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

                if (!string.IsNullOrWhiteSpace(contentResponse))
                {
                    //chuyển sang C# object để service access đc 
                    return JsonSerializer.Deserialize<ModerationAiResponse>(contentResponse, _jsonOptions);
                }
            }
            catch
            {
                // fall through to default handling
            }

            return null;
        }

        //dựng response từ AI 
        private static string? BuildExplanationFromAi(ModerationProfile profile, double score, string? decision, ModerationAiExplanation? explanation)
        {
            var english = explanation?.English;
            var vietnamese = explanation?.Vietnamese;

            //chỉ sử dụng explanation AI nếu đủ cả english và vn trong response 
            if (!string.IsNullOrWhiteSpace(english) && !string.IsNullOrWhiteSpace(vietnamese))
            {
                var headerEn = $"Automated Score: {score:0.00}/10.00";
                var headerVn = $"Điểm tự động: {score:0.00}/10.00";
                return $"English:\n{headerEn}\n{english.Trim()}\n\nTiếng việt:\n{headerVn}\n{vietnamese.Trim()}";
            }

            //check trong decision của AI có rejected hay là auto approved để build explanation cuối 
            var shouldReject = string.Equals(decision, "rejected", StringComparison.OrdinalIgnoreCase);
            var autoApproved = string.Equals(decision, "auto_approved", StringComparison.OrdinalIgnoreCase);
            return BuildDecisionExplanation(profile, score, shouldReject, autoApproved);
        }
        //cái này để hard response khi AI failed hoặc response sai format 
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

            return $"English:\n{english}\n\nTiếng việt:\n{vietnamese}";
        }

        //tạo hình AI cover 
        public async Task<OpenAiImageResult> GenerateCoverAsync(string prompt, CancellationToken ct = default)
        {
            //lấy model trong appsettings, lấy prompt từ request của author với size mặc định (ko set nhỏ hơn đc, tối thiểu 1024x1024)
            var payload = new ImageRequest
            {
                Model = _settings.ImageModel,
                Prompt = prompt,
                Size = "1024x1024"
            };

            //gọi api tạo hình 
            using var response = await _httpClient.PostAsJsonAsync("images/generations", payload, cancellationToken: ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI image generation failed with status {(int)response.StatusCode}: {body}");
            }
            //check result có ra đc hình ko hay null 
            var result = await response.Content.ReadFromJsonAsync<ImageResponse>(cancellationToken: ct)
                         ?? throw new InvalidOperationException("OpenAI image generation returned an empty response.");

            //ví dụ nó ra nhiều ảnh thì lấy ảnh đầu (ko xảy ra, để cái này dự phòng thôi) 
            var image = result.Data?.FirstOrDefault()
                        ?? throw new InvalidOperationException("OpenAI image generation did not return any images.");

            Stream stream;
            string fileName;
            string contentType;

            //bắt đầu chekc các trường hợp response: 

            //th1: trả về base64 
            if (!string.IsNullOrWhiteSpace(image.Base64Data))
            {
                var bytes = Convert.FromBase64String(image.Base64Data);
                stream = new MemoryStream(bytes, writable: false);
                fileName = $"story_cover_{Guid.NewGuid():N}.png";
                contentType = "image/png";
            }
            //th2: trả url
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

        public async Task<string> DetectMoodAsync(string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return "neutral";
            }

            var systemPrompt = $"You are a literary emotion classifier. Read the passage and respond with JSON only: {{ \"mood\": one_of[{string.Join(",", MoodCodes)}] }}";
            var payload = new ChatCompletionsRequest
            {
                Model = _settings.ChatModel,
                Temperature = 0,
                MaxTokens = 60,
                Messages = new[]
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = content }
                }
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
                request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return "neutral";
                }

                var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct);
                var contentResponse = envelope?.Choices?
                    .Select(c => c.Message?.Content)
                    .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

                if (string.IsNullOrWhiteSpace(contentResponse))
                {
                    return "neutral";
                }

                var parsed = JsonSerializer.Deserialize<MoodDetectionResponse>(contentResponse, _jsonOptions);
                var mood = parsed?.Mood?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(mood))
                {
                    return "neutral";
                }

                return MoodCodes.Contains(mood, StringComparer.OrdinalIgnoreCase) ? mood : "neutral";
            }
            catch
            {
                return "neutral";
            }
        }

        public async Task<string> ChatAsync(IReadOnlyList<AiChatPromptMessage> messages, CancellationToken ct = default)
        {
            if (messages == null || messages.Count == 0)
            {
                throw new ArgumentException("Conversation history is required.", nameof(messages));
            }

            var payload = new ChatCompletionsRequest
            {
                Model = _settings.ChatModel,
                Temperature = 0.2,
                MaxTokens = 700,
                Messages = messages
                    .Select(m => new ChatMessage
                    {
                        Role = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role,
                        Content = m.Content
                    })
                    .ToArray()
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                var failureBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI chat request failed with status {(int)response.StatusCode}: {failureBody}");
            }

            var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct)
                          ?? throw new InvalidOperationException("OpenAI chat response was empty.");

            var reply = envelope.Choices?
                .Select(c => c.Message?.Content)
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

            if (string.IsNullOrWhiteSpace(reply))
            {
                throw new InvalidOperationException("OpenAI chat response did not contain any content.");
            }

            return reply.Trim();
        }

        public async Task<string> SummarizeChapterAsync(string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var systemPrompt = "You are a literary editor. Read the provided chapter content and generate a concise summary (approx. 100-120 words). IMPORTANT: Detect the language of the content and write the summary in that EXACT same language. Do not add any introductory phrases like 'Here is the summary:',just return the summary and no more.";
            var payload = new ChatCompletionsRequest
            {
                Model = _settings.ChatModel,
                Temperature = 0.3,
                MaxTokens = 200,
                Messages = new[]
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = content }
                }
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
                request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty; // Return empty on failure to avoid blocking the user flow
                }

                var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct);
                var summary = envelope?.Choices?
                    .Select(c => c.Message?.Content)
                    .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

                return summary?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty; // Fail silently
            }
        }

        public async Task<List<string>> ExtractKeywordsAsync(string userQuery, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userQuery)) return new List<string>();

            var systemPrompt = "You are a search query expander for a storytelling database. Your goal is to bridge the gap between user intent and database content (stories, chapters, authors).\n" +
                               "Analyze the user's query and generate a comprehensive list of 5-10 search terms. Include:\n" +
                               "1. Exact important keywords from the query (names, titles).\n" +
                               "2. Synonyms and related concepts (e.g. 'animal' -> 'pet, beast, dog, cat, zoo').\n" +
                               "3. English translations if the query is non-English, and vice-versa (e.g. 'hồi quy' -> 'regressor, returnee').\n" +
                               "Return ONLY a comma-separated list of strings. Do not explain.";
            
            var payload = new ChatCompletionsRequest
            {
                Model = _settings.ChatModel,
                Temperature = 0.5, // Slightly higher creativity for synonyms
                MaxTokens = 150,
                Messages = new[]
                {
                    new ChatMessage { Role = "system", Content = systemPrompt },
                    new ChatMessage { Role = "user", Content = userQuery }
                }
            };

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
                request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode) return new List<string> { userQuery };

                var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionsResponse>(_jsonOptions, ct);
                var content = envelope?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrWhiteSpace(content)) return new List<string> { userQuery };

                return content.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(k => k.Trim())
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string> { userQuery };
            }
        }

        private static string ComposeContent(string title, string? description, string? outline)
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

            if (!string.IsNullOrWhiteSpace(outline))
            {
                builder.AppendLine(outline.Trim());
            }

            return builder.Length > 0 ? builder.ToString().Trim() : string.Empty;
        }

        private sealed record ModerationProfile(
            string ContentType,
            string PrimaryLabel,
            string SecondaryLabel,
            string? TertiaryLabel);

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

            [JsonPropertyName("penalty")]
            public double? Penalty { get; init; }
        }

        private sealed record ModerationAiExplanation
        {
            [JsonPropertyName("english")]
            public string? English { get; init; }

            [JsonPropertyName("vietnamese")]
            public string? Vietnamese { get; init; }
        }

        private sealed record MoodDetectionResponse
        {
            [JsonPropertyName("mood")]
            public string? Mood { get; init; }
        }
    }
}


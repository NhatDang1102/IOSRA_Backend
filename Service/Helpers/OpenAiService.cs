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
                category = "Language Compliance",
                labels = new[] { "wrong_language" },
                penalties = new[] { "-10.0: The primary language of the content is completely different from the required languageCode." },
                examples = "E.g.: Content is in Japanese or English but languageCode is 'vi-VN'.",
                rules = "ONLY use this if the content belongs to a DIFFERENT language. DO NOT use this for poor grammar, spelling errors, or 'teen code' if it is still the correct base language.",
                note = "Mismatching the language family results in immediate 0.0 score."
            },
            new
            {
                category = "URL / External Redirect",
                labels = new[] { "url_redirect" },
                penalties = new[] { "-1.5: Per unique link, social media invite, or call-to-action to leave the platform." },
                examples = "E.g.: http, https, www, .com, .net, bit.ly, telegram, discord.gg, invite codes, 'Follow me on Facebook/Patreon'.",
                rules = "Detect any attempt to redirect readers. Brand mentions without links are usually 0.",
                note = "Subtract 1.5 points for every instance found."
            },
            new
            {
                category = "Spam / Nonsense / Gibberish",
                labels = new[] { "spam_repetition" },
                penalties = new[] { 
                    "-10.0: The content is primarily gibberish, keyboard smashing, or has NO meaningful prose (e.g. 'abcxyz...', 'hshshs').",
                    "-4.0: Heavy spam, long sequences of nonsense, or filler text blocks.",
                    "-1.0: Short bursts of repetitive tokens."
                },
                examples = "E.g.: 'up up up', 'aaaaaaaa', 'test test test', 'next next next... aaaaa...', 'xyzba abznx', 'asdfghjkl'.",
                rules = "Detect placeholder text, keyboard smashing, or repetitive phrases used to inflate word count.",
                note = "Ensures content has meaningful prose."
            },
            new
            {
                category = "Explicit Sexual Content",
                labels = new[] { "sexual_explicit", "sexual_minor", "sexual_transaction", "fetish_extreme" },
                penalties = new[] { 
                    "-3.0: Graphic sexual acts, explicit NSFW descriptions, or non-consensual content.",
                    "-1.5: Detailed nudity, heavy sexual innuendo, or fetish focus.",
                    "-0.5: Mild romantic intimacy with borderline sexual descriptions."
                },
                examples = "E.g.: Descriptions of intercourse, sexual organs, or forced acts.",
                note = "CRITICAL: Any sexual content involving minors results in immediate rejection (score < 5)."
            },
            new
            {
                category = "Violent / Extremist",
                labels = new[] { "violent_gore", "extremist_rhetoric" },
                penalties = new[] { 
                    "-3.0: Graphic torture, excessive gore, or terrorist/extremist propaganda.",
                    "-1.5: Praising violence, glorifying murder, or encouraging harm.",
                    "-0.5: Narrative combat with moderate blood or injury descriptions."
                },
                examples = "E.g.: Terrorist manifestos, detailed descriptions of dismemberment, or glorifying mass shootings.",
                note = "Narrative action is allowed, but glorification of extremist violence is not."
            },
            new
            {
                category = "Hate Speech / Harassment",
                labels = new[] { "hate_speech", "harassment_targeted", "mild_insult" },
                penalties = new[] { 
                    "-3.0: Hate speech against protected classes (race, religion, gender, etc.) or slurs.",
                    "-2.0: Targeted harassment or cyberbullying.",
                    "-0.5: Mild insults or toxic behavior."
                },
                examples = "E.g.: Racial slurs, calls for discrimination, or attacking real-world individuals.",
                note = "Zero tolerance for hate speech against protected groups."
            },
            new
            {
                category = "Self-harm & Suicide",
                labels = new[] { "self_harm_promotion", "self_harm_instruction", "self_harm_neutral" },
                penalties = new[] { 
                    "-3.0: Promoting, glorifying, or providing instructions for self-harm or suicide.",
                    "-1.0: Neutral/narrative mention in an encouraging context."
                },
                examples = "E.g.: 'How to cut yourself...', glorifying suicide as a solution.",
                note = "Never allow content that encourages or teaches self-harm."
            },
            new
            {
                category = "Illegal Activities",
                labels = new[] { "illegal_instruction", "illegal_promotion" },
                penalties = new[] { 
                    "-2.5: Actionable 'how-to' guides for drugs, hacking, or weapons.",
                    "-1.0: Glorifying criminal behavior.",
                    "-0.5: Incidental narrative mention."
                },
                examples = "E.g.: Drug manufacturing recipes, instructions on how to bypass security/locks.",
                note = "Distinguish between narrative fiction and real-world illegal instructions."
            },
            new
            {
                category = "Personal Data / Doxxing",
                labels = new[] { "personal_data" },
                penalties = new[] { "-2.5: Exposing real-world private info (phone, address, ID, bank info)." },
                examples = "E.g.: 'Call me at 0901234567', 'My home address is...', 'ID number: 123...'.",
                note = "Applies to real-world sensitive data exposure."
            },
            new
            {
                category = "Low Quality / Irrelevant",
                labels = new[] { "low_quality", "irrelevant_ad" },
                penalties = new[] { 
                    "-1.5: Advertisements, promotional text, or placeholder text.",
                    "-0.5: Mildly off-topic content."
                },
                examples = "E.g.: 'Buy Bitcoin now', 'Visit this shop for 50% off'.",
                note = "Target content used for external advertising."
            },
            new
            {
                category = "Writing Quality",
                labels = new[] { "grammar_spelling", "poor_formatting", "weak_prose" },
                penalties = new[] { 
                    "-0.5: Frequent typos, lack of proper tone/accents (e.g., 'toi di choi').",
                    "-0.5: Poor formatting, 'wall of text' (long paragraphs), or excessive capitalization.",
                    "-0.25: Extremely weak or excessively repetitive prose."
                },
                examples = "E.g.: No line breaks for 500+ words, constant misspelling, or 'ALL CAPS' text.",
                note = "Penalize amateurish writing that degrades the reading experience."
            },
            new
            {
                category = "Inconsistent Content",
                labels = new[] { "inconsistent_content" },
                penalties = new[] { "-3.0: Title, description, and outline are unrelated or contradictory." },
                examples = "E.g.: Title is 'Modern High School' but outline is about 'Ancient Space Cults'.",
                rules = "Check if all provided elements (Title, Description, Outline) match conceptually.",
                note = "Apply only when story elements do not match."
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

        public Task<OpenAiModerationResult> ModerateStoryAsync(string title, string? description, string outline, string languageCode, CancellationToken ct = default)
            => ModerateContentAsync(title, description, outline, StoryProfile, languageCode, ct);

        public Task<OpenAiModerationResult> ModerateChapterAsync(string title, string content, string languageCode, CancellationToken ct = default)
            => ModerateContentAsync(title, content, null, ChapterProfile, languageCode, ct);


        //hàm quét AI kiểm duyệt 
        private async Task<OpenAiModerationResult> ModerateContentAsync(string primary, string? secondary, string? tertiary, ModerationProfile profile, string? languageCode, CancellationToken ct)
        {

            //gắn hết content vô 1 chuỗi để request cho openai 
            var combined = ComposeContent(primary, secondary, tertiary);
            var ai = await RequestModerationAsync(profile, combined, languageCode, ct);

            //tính điểm trực tiếp (ko để AI tính vì tính sai)
            var totalDeduction = ai?.Violations?.Sum(v => Math.Abs(v.Penalty ?? 0)) ?? 0;
            var rawScore = 10.0 - totalDeduction;
            
            if (rawScore > 9.5) rawScore = 9.5;

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
        private async Task<ModerationAiResponse?> RequestModerationAsync(ModerationProfile profile, string content, string? languageCode, CancellationToken ct)
        {
            //define system openAI 
            var moderationInstructions = @"Return JSON only with shape { ""score"": number, ""decision"": ""auto_approved|pending_manual_review|rejected"", ""violations"": [{ ""label"": string, ""evidence"": [string], ""penalty"": number }], ""explanation"": { ""vietnamese"": string } }.
Start from base score = 10.00. 

STRICT PENALTY RULES:
- Penalties are CUMULATIVE and PER OCCURRENCE.
- For each violation, the ""penalty"" field MUST be calculated as: (Penalty Amount from table) * (Number of occurrences in the content).
- Example: If ""spam_repetition"" is 1.5 and you find 3 snippets of spam, the ""penalty"" MUST be 4.5.
- Example: If ""url_redirect"" is 1.5 and you find 2 links, the ""penalty"" MUST be 3.0.
- You MUST list all offending snippets in the ""evidence"" array.
- The final ""score"" field in the JSON MUST be exactly 10.00 minus the sum of all ""penalty"" values in the ""violations"" array.

Rules that must always be enforced:
- A `languageCode` field (e.g., 'en-US', 'vi-VN') is provided. This is the REQUIRED language.
- CHECK if the content language matches the `languageCode`.
- ONLY use ""wrong_language"" if the content is in a COMPLETELY DIFFERENT language family (e.g., Russian text for 'en-US').
- IF content matches `languageCode` (e.g. Japanese content with 'ja-JP'), you MUST NOT use ""wrong_language"".
- IMPORTANT: If the content is in the CORRECT language but has many spelling mistakes, bad grammar, or slang, DO NOT use ""wrong_language"". Instead, use ""grammar_spelling"" and ""weak_prose"" from the deductions table.
- A few words/phrases in another language are acceptable (eg., names, locations).
- Use the provided ""deductions"" table for labels and base penalty amounts.
- Maximum allowed score for any submission is 9.5 (even with no violations).

Decision Mapping (STRICT):
- score >= 7.0 AND no forced rejection labels => ""auto_approved"".
- 5.0 <= score < 7.0 => ""pending_manual_review"".
- score < 5.0 OR any forced rejection labels => ""rejected"".

Explanation requirements:
- Provide a detailed Vietnamese summary. The explanation MUST align with the score and decision.
- DO NOT state the final score in the text; the system handles the display.
- CRITICAL: The 'vietnamese' field in the JSON is MANDATORY and CANNOT be empty or null.";

            //gom hết phạm vi luật, ngưỡng điểm, instruction vào 1 payload 
            var userPayload = new
            {
                contentType = profile.ContentType,
                languageCode = languageCode,
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
                MaxTokens = 1000,
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
            var vietnamese = explanation?.Vietnamese;

            //chỉ sử dụng explanation AI nếu có tiếng Việt
            if (!string.IsNullOrWhiteSpace(vietnamese))
            {
                var headerVn = $"Điểm tự động: {score:0.00}/10.00";
                return $"{headerVn}\n{vietnamese.Trim()}";
            }

            //check trong decision của AI có rejected hay là auto approved để build explanation cuối 
            var shouldReject = string.Equals(decision, "rejected", StringComparison.OrdinalIgnoreCase);
            var autoApproved = string.Equals(decision, "auto_approved", StringComparison.OrdinalIgnoreCase);
            return BuildDecisionExplanation(profile, score, shouldReject, autoApproved);
        }
        //cái này để hard response khi AI failed hoặc response sai format 
        private static string BuildDecisionExplanation(ModerationProfile profile, double score, bool shouldReject, bool autoApproved)
        {
            if (shouldReject)
                return $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), thấp hơn {ManualReviewThreshold:0.00} nên nội dung {profile.ContentType} bị từ chối.";
            
            if (autoApproved)
                return $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), đạt ngưỡng tự duyệt {AutoApproveThreshold:0.00} nên nội dung {profile.ContentType} được xuất bản.";

            return $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), thấp hơn {AutoApproveThreshold:0.00} nhưng không dưới {ManualReviewThreshold:0.00} nên nội dung {profile.ContentType} chuyển cho moderator.";
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

            var systemPrompt = $"You are a professional literary translator. Translate from {sourceLanguageCode} to {targetLanguageCode}. Preserve the author's tone and paragraph breaks. IMPORTANT: The content may contain formatting tags, markup, or special technical symbols. You MUST PRESERVE ALL such tags and symbols EXACTLY as they appear in the source text. Do not translate, remove, or modify any tags. Ensure the translated text is correctly placed within the original formatting structure. Return ONLY the translated content with no additional commentary.";
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


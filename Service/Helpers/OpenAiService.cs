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
            "Detect any sharing of sensitive numbers like phone numbers, bank accounts, or addresses, even if they appear fictional or part of a story.",
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
                penalties = new[] { "-10.0: The primary language of the content DOES NOT MATCH the required languageCode." },
                examples = "E.g.: Content is Vietnamese but languageCode is 'ja-JP'.",
                rules = "ONLY trigger if the DOMINANT language is wrong (e.g. Japanese text for Vietnamese code). ABSOLUTELY ALLOW Proper Nouns, BRANDS (e.g. Facebook, Google, iPhone), and loanwords. If the text is readable in the target language, DO NOT PENALIZE.",
                note = "Mismatching the language results in immediate 0.0 score."
            },
            new
            {
                category = "URL / External Redirect",
                labels = new[] { "url_redirect" },
                penalties = new[] { "-1.5: Per unique link, social media invite, or call-to-action to leave the platform." },
                examples = "E.g.: http, https, www, .com, .net, bit.ly, telegram, discord.gg.",
                rules = "Detect any attempt to redirect readers. Brand mentions without links are usually 0.",
                note = "Subtract 1.5 points for every instance found."
            },
            new
            {
                category = "Spam / Gibberish",
                labels = new[] { "spam_repetition" },
                penalties = new[] { "-10.0: Gibberish, keyboard smashing, or excessive repetition." },
                examples = "E.g.: 'xybza', 'asdfgh', 'string string', 'un ummm unnn', 'hả hả hả'.",
                rules = "AGGRESSIVELY penalize random character strings (e.g. 'xybza'), excessive meaningless filler sounds (e.g. 'un ummm unnn' repeated), or copying the same paragraph >3 times. If it looks like test data, REJECT.",
                note = "Zero tolerance for gibberish or low-effort filler."
            },
            new
            {
                category = "Sexual - Minor / Violence",
                labels = new[] { "sexual_forbidden" },
                penalties = new[] { "-10.0: Sexual content involving minors (CSAM), rape, or severe degradation." },
                examples = "E.g.: Child abuse, non-consensual acts.",
                note = "Immediate rejection."
            },
            new
            {
                category = "Sexual - Explicit",
                labels = new[] { "sexual_explicit" },
                penalties = new[] { "-3.0: Graphic sexual acts, explicit NSFW descriptions." },
                examples = "E.g.: Detailed intercourse descriptions.",
                note = "Heavy penalty for explicit content."
            },
            new
            {
                category = "Sexual - Borderline",
                labels = new[] { "sexual_borderline" },
                penalties = new[] { "-1.0: Heavy innuendo, fetish focus, or detailed nudity without explicit acts." },
                examples = "E.g.: Intense make-out sessions, focus on body parts.",
                note = "Moderate penalty."
            },
            new
            {
                category = "Violent - Extremist",
                labels = new[] { "violent_extremist" },
                penalties = new[] { "-10.0: Terrorist propaganda, glorifying mass harm." },
                examples = "E.g.: Manifestos, hate crimes.",
                note = "Immediate rejection."
            },
            new
            {
                category = "Violent - Graphic",
                labels = new[] { "violent_graphic" },
                penalties = new[] { "-3.0: Detailed torture, excessive gore, or sadism." },
                examples = "E.g.: Slow dismemberment descriptions.",
                note = "Heavy penalty."
            },
             new
            {
                category = "Violent - Moderate",
                labels = new[] { "violent_moderate" },
                penalties = new[] { "-1.0: Excessive blood in combat, or glorifying injury." },
                examples = "E.g.: Detailed fight scenes with heavy bleeding.",
                note = "Moderate penalty."
            },
            new
            {
                category = "Hate Speech - Severe",
                labels = new[] { "hate_speech_severe" },
                penalties = new[] { "-3.0: Slurs, hate speech against protected classes." },
                examples = "E.g.: Racial slurs, religious hatred.",
                note = "Strictly penalized."
            },
            new
            {
                category = "Harassment - Targeted",
                labels = new[] { "harassment_targeted" },
                penalties = new[] { "-2.0: Bullying or attacking specific individuals." },
                examples = "E.g.: Doxxing threats, cyberbullying.",
                note = "Targeted attacks."
            },
             new
            {
                category = "Insults - Mild",
                labels = new[] { "insult_mild" },
                penalties = new[] { "-0.5: Toxic behavior, name-calling." },
                examples = "E.g.: 'Idiot', 'Stupid'.",
                note = "Minor penalty."
            },
            new
            {
                category = "Self-harm - Promotion",
                labels = new[] { "self_harm_promo" },
                penalties = new[] { "-10.0: Encouraging or instructing on self-harm/suicide." },
                examples = "E.g.: 'How to cut veins'.",
                note = "Immediate rejection."
            },
            new
            {
                category = "Illegal - Instruction",
                labels = new[] { "illegal_instruction" },
                penalties = new[] { "-2.5: Guides for crimes (drugs, bombs, hacking)." },
                examples = "E.g.: Meth recipes.",
                note = "Safety violation."
            },
            new
            {
                category = "Personal Data",
                labels = new[] { "personal_data" },
                penalties = new[] { "-2.5: Any numeric strings resembling phone numbers, bank accounts, or credit cards." },
                examples = "E.g.: 0901234567, 123456789.",
                rules = "Trigger this for ANY sensitive-looking numbers. DO NOT allow them even if they are described as fictional or as part of a dialogue. Zero tolerance for exposing numeric identifiers.",
                note = "Privacy violation."
            },
            new
            {
                category = "Low Quality - Template",
                labels = new[] { "low_quality_template" },
                penalties = new[] { "-10.0: Raw templates, placeholders like 'Content updating', 'Coming soon', 'Insert Text Here'." },
                examples = "E.g.: 'Nội dung đang cập nhật', 'Chương này sẽ có sau', 'Lorem Ipsum'.",
                rules = "REJECT if the content is just a placeholder promise (e.g., 'will update later', 'chưa có nội dung') instead of actual story content. Short announcements are NOT valid chapters.",
                note = "Immediate rejection."
            },
             new
            {
                category = "Low Quality - Filler",
                labels = new[] { "low_quality_filler" },
                penalties = new[] { "-5.0: Extremely short (< 50 words) without substance, or just a list of keywords." },
                examples = "E.g.: Just tags, 'abc xyz', or very short meaningless text.",
                rules = "If content is too short to be a story chapter (< 30 words) and lacks narrative/dialogue, flag as filler. A story chapter implies narration.",
                note = "Rejection."
            },
             new
            {
                category = "Writing - Formatting",
                labels = new[] { "poor_formatting" },
                penalties = new[] { "-0.5: Wall of text, no line breaks, all caps." },
                examples = "E.g.: 500 words in one block.",
                note = "Quality control."
            },
            new
            {
                category = "Inconsistent Content",
                labels = new[] { "inconsistent_content" },
                penalties = new[] { "-3.0: Title/Outline contradictory." },
                examples = "E.g.: Title 'Space' vs Outline 'Farming'.",
                rules = "Check if all provided elements (Title, Description, Outline) match conceptually.",
                note = "Coherence check."
            }
        };

        //define các mood để tạo nhạc
        private static readonly string[] MoodCodes = { "calm", "sad", "mysterious", "excited", "romantic", "neutral" };

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
            //Logic mới: AI trả về base penalty, Code tự nhân với số lượng bằng chứng (Evidence Count)
            var totalDeduction = ai?.Violations?.Sum(v => 
            {
                var count = (v.Evidence != null && v.Evidence.Length > 0) ? v.Evidence.Length : 1;
                return Math.Abs(v.Penalty ?? 0) * count;
            }) ?? 0;

            var rawScore = 10.0 - totalDeduction;
            
            if (rawScore > 9.5) rawScore = 9.5;

            var score = Math.Clamp(Math.Round(rawScore, 2, MidpointRounding.AwayFromZero), 0.0, 10.0);
            
            // Strict threshold logic:
            // Score < 5.0 => Rejected
            // 5.0 <= Score <= 7.0 => Pending Manual Review
            // Score > 7.0 => Auto Approved

            bool shouldReject = score < ManualReviewThreshold;
            bool autoApproved = score > AutoApproveThreshold;

            var normalizedDecision = shouldReject ? "rejected" : (autoApproved ? "auto_approved" : "pending_manual_review");

            var violations = ai?.Violations?
                .Select(v =>
                {
                    var label = string.IsNullOrWhiteSpace(v.Label) ? "violation" : v.Label;
                    var evidence = v.Evidence ?? Array.Empty<string>();
                    var count = Math.Max(1, evidence.Length);
                    var basePenalty = v.Penalty ?? 0.0;
                    // Penalty trả về client là tổng phạt cho nhóm vi phạm này
                    return new ModerationViolation(label, count, evidence, basePenalty * count);
                })
                .ToArray() ?? Array.Empty<ModerationViolation>();

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
- For each violation, the ""penalty"" field MUST be the **BASE PENALTY** for a SINGLE occurrence (from the table).
- DO NOT multiply by the number of occurrences. The system will automatically multiply (Base Penalty * Evidence Count).
- Example: If ""url_redirect"" is 1.5 and you find 2 links, set ""penalty"" to 1.5 (NOT 3.0).
- You MUST list all offending snippets in the ""evidence"" array.
- The final ""score"" field in the JSON is for display only; the system will recalculate it.

Rules that must always be enforced:
- A `languageCode` field (e.g., 'en-US', 'vi-VN', 'ja-JP') is provided. This is the REQUIRED language.
- STEP 1: DETECT the **DOMINANT** language of the provided content.
- STEP 2: COMPARE the **DOMINANT** language with `languageCode`.
- IF the **DOMINANT** language is completely different (e.g. `languageCode` is 'ja-JP' but content is Vietnamese), you MUST trigger the ""wrong_language"" violation (-10.0 points).
- **CRITICAL EXCEPTION**: If the content contains Proper Nouns, **BRANDS** (e.g. 'Facebook', 'Google', 'YouTube'), loanwords, or short phrases, but the **DOMINANT** grammar matches `languageCode`, THIS IS VALID. DO NOT flag as ""wrong_language"".
- IMPORTANT: If the content is in the CORRECT language but has many spelling mistakes, bad grammar, or slang, DO NOT use ""wrong_language"". Instead, use ""grammar_spelling"" and ""weak_prose"".
- Use the provided ""deductions"" table for labels and base penalty amounts.
- Maximum allowed score for any submission is 9.5 (even with no violations).

CONSISTENCY CHECK: 
- Your calculated ""score"" and your ""explanation"" MUST mathematically align with the ""violations"" array.
- NEVER write a positive explanation (e.g. ""Good content"") if you have included a -10.0 penalty in ""violations"".
- If you decide the content is valid (e.g. valid use of loanwords per the exception), you MUST NOT include the ""wrong_language"" violation in the array at all.
- If violations exist, the score MUST reflect the deduction.

Decision Mapping (STRICT):
- score > 7.0 => ""auto_approved"".
- 5.0 <= score <= 7.0 => ""pending_manual_review"".
- score < 5.0 => ""rejected"".

Explanation requirements:
- Provide a detailed Vietnamese summary. The explanation MUST align with the score and decision.
- IMPORTANT: Do NOT use English labels/tags (e.g. 'url_redirect', 'violent_moderate') in the explanation text. Translate them into natural Vietnamese descriptions (e.g. 'chứa liên kết ngoài', 'nội dung bạo lực').
- Translate the final decision status into Vietnamese context naturally (e.g. 'bị từ chối', 'cần xem xét', 'đã duyệt').
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
                return $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), lớn hơn {AutoApproveThreshold:0.00} nên nội dung {profile.ContentType} được xuất bản.";

            return $"Điểm kiểm duyệt tự động là {score:0.00}/10 sau khi áp dụng các mức trừ (không có chi tiết), từ {ManualReviewThreshold:0.00} đến {AutoApproveThreshold:0.00} nên nội dung {profile.ContentType} chuyển cho moderator.";
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


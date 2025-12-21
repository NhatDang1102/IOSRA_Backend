using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Voice;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Service.Helpers;
using Contract.DTOs.Response.Chapter;

namespace Service.Services
{
    public class AuthorChapterService : IAuthorChapterService
    { //số chữ min khi up chap 
        private const int MinContentLength = 200;
        private const int MaxContentLength = 10000;
        private readonly IAuthorChapterRepository _chapterRepository;
        private readonly IAuthorStoryRepository _storyRepository;
        private readonly IChapterContentStorage _contentStorage;
        private readonly IOpenAiModerationService _openAiModerationService;
        private readonly IFollowerNotificationService _followerNotificationService;
        private readonly IChapterPricingService _chapterPricingService;
        private readonly IProfileRepository? _profileRepository;
        //định ra các status cho phép
        private static readonly string[] AuthorChapterAllowedStatuses = { "draft", "pending", "rejected", "published", "hidden", "removed" };
        private static readonly string[] ChapterAccessTypes = { "free", "dias" };

        public AuthorChapterService(
            IAuthorChapterRepository chapterRepository,
            IAuthorStoryRepository storyRepository,
            IChapterContentStorage contentStorage,
            IOpenAiModerationService openAiModerationService,
            IFollowerNotificationService followerNotificationService,
            IChapterPricingService chapterPricingService,
            IProfileRepository? profileRepository = null)
        {
            _chapterRepository = chapterRepository;
            _storyRepository = storyRepository;
            _contentStorage = contentStorage;
            _openAiModerationService = openAiModerationService;
            _followerNotificationService = followerNotificationService;
            _chapterPricingService = chapterPricingService;
            _profileRepository = profileRepository;
        }

        public async Task<ChapterResponse> CreateAsync(Guid authorAccountId, Guid storyId, ChapterCreateRequest request, CancellationToken ct = default)
        {
            //check author profile 
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả chưa được đăng ký.", 404);


            //restricted thi ko đc upload chapter mới 
            await AccountRestrictionHelper.EnsureCanPublishAsync(author.account, _profileRepository, ct);

            var story = await _storyRepository.GetByIdForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            if (!string.Equals(story.status, "published", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(story.status, "hidden", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryNotPublished", "Chương chỉ có thể được tạo khi truyện đã xuất bản hoặc bị ẩn.", 400);
            }

            var lastRejectedAt = await _chapterRepository.GetLastAuthorChapterRejectedAtAsync(author.account_id, ct);
            //if (lastRejectedAt.HasValue && lastRejectedAt.Value > TimezoneConverter.VietnamNow.AddHours(-24))
            //{
            //    throw new AppException("ChapterCreationCooldown", "Bạn phải đợi 24 giờ sau khi chương bị từ chối trước khi tạo chương mới.", 400, new
            //    {
            //        availableAtUtc = lastRejectedAt.Value.AddHours(24)
            //    });
            //}

            //if (await _chapterRepository.HasPendingChapterAsync(story.story_id, ct))
            //{
            //    throw new AppException("ChapterPendingExists", "Một chương đang chờ kiểm duyệt cho truyện này.", 400);
            //}

            var title = (request.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new AppException("InvalidChapterTitle", "Tiêu đề chương không được để trống.", 400);
            }

            var content = (request.Content ?? string.Empty).Trim();
            if (content.Length < MinContentLength)
            {
                throw new AppException("ChapterContentTooShort", $"Nội dung chương phải có ít nhất  {MinContentLength} kí tự.", 400);
            }
            if (content.Length > MaxContentLength)
            {
                throw new AppException("ChapterContentTooLong", $"Nội dung chương tối đa {MaxContentLength} kí tự.", 400);
            }
            //đếm số TỪ (số từ ko phải số kí tự)  
            var wordCount = CountWords(content);
            if (wordCount <= 0)
            {
                throw new AppException("ChapterContentEmpty", "Không được để trống nội dung.", 400);
            }
            //đếm số KÍ TỰ
            var charCount = content.Length;
            //định giá 
            var price = await _chapterPricingService.GetPriceAsync(charCount, ct);
            //định no. của chapter 
            var chapterNumber = await _chapterRepository.GetNextChapterNumberAsync(story.story_id, ct);
            var chapterId = Guid.NewGuid();
            var requestedAccessType = NormalizeOptionalChapterAccessType(request.AccessType);
            //check coi rank causal hay 3 rank sponsored
            var canLockChapters = CanAuthorLockChapters(author, story);
            var accessType = ResolveAccessTypeForCreate(requestedAccessType, canLockChapters, story.is_premium);
            //mood mặc định là neutral (lúc submit mới coi mood)
            var chapter = new chapter
            {
                chapter_id = chapterId,
                story_id = story.story_id,
                chapter_no = (uint)chapterNumber,
                title = title,
                summary = null,
                dias_price = (uint)price,
                access_type = accessType,
                content_url = null,
                mood_code = "neutral",
                word_count = wordCount,
                char_count = charCount,
                status = "draft",
                created_at = TimezoneConverter.VietnamNow,
                updated_at = TimezoneConverter.VietnamNow,
                submitted_at = null,
                published_at = null
            };

            string contentKey;
            try
            {
                //đẩy story id với chapter id để up lên bucket cloud 
                contentKey = await _contentStorage.UploadAsync(story.story_id, chapter.chapter_id, content, ct);
                chapter.content_url = contentKey;
                await _chapterRepository.CreateAsync(chapter, ct);

            }
            catch
            {
                //nếu k lưu db đc thì delete khỏi cloud để tránh file rác 
                if (!string.IsNullOrWhiteSpace(chapter.content_url))
                {
                    await _contentStorage.DeleteAsync(chapter.content_url, ct);
                }
                throw;
            }

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        public async Task<IReadOnlyList<ChapterListItemResponse>> GetAllAsync(Guid authorAccountId, Guid storyId, string? status, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả chưa được đăng ký.", 404);

            var story = await _storyRepository.GetByIdForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            var filterStatuses = NormalizeChapterStatuses(status);
            var chapters = await _chapterRepository.GetAllByStoryAsync(story.story_id, filterStatuses, ct);
            return chapters.Select(MapChapterListItem).ToArray();
        }

        public async Task<ChapterResponse> GetByIdAsync(Guid authorAccountId, Guid storyId, Guid chapterId, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả chưa được đăng ký.", 404);

            var chapter = await _chapterRepository.GetByIdForAuthorAsync(storyId, chapterId, author.account_id, ct)
                          ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        public async Task<ChapterResponse> SubmitAsync(Guid authorAccountId, Guid chapterId, ChapterSubmitRequest request, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả chưa được đăng ký.", 404);

            var chapter = await _chapterRepository.GetByIdAsync(chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            //báo lỗi nếu k lấy info story 
            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            if (story.author_id != author.account_id)
            {
                throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);
            }

            //nghiệp vụ hiện tại: hidden cho đăng draft mới NHƯNG không submit được 
            if (string.Equals(story.status, "hidden", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryHidden", "Truyện đang bị ẩn, không thể submit chương mới.", 400);
            }
            //chỉ đc 1 truyện pending cùng 1 lúc 
            if (await _chapterRepository.HasPendingChapterAsync(story.story_id, ct) &&
                !string.Equals(chapter.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterPendingExists", "Đang có chương khác đang ở trạng thái đang chờ, hãy submit chương đó trước.", 400);
            }

            if (!string.Equals(chapter.status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidChapterState", "Chỉ có chương nháp mới có thể submit.", 400);
            }
            //phải submit theo đúng thứ tự draft 
            var hasEarlierDraft = await _chapterRepository.HasDraftChapterBeforeAsync(story.story_id, chapter.created_at, chapter.chapter_id, ct);
            if (hasEarlierDraft)
            {
                throw new AppException("ChapterSubmissionOrder", "Vui lòng submit theo thứ tự.", 400);
            }

            if (string.IsNullOrWhiteSpace(chapter.content_url))
            {
                throw new InvalidOperationException("Nội dung trống.");
            }

            //lấy content trên cloud xuống rồi đổi lại sang string 
            var content = await _contentStorage.DownloadAsync(chapter.content_url, ct);
            //lúc submit mới gọi luôn openAI chấm cảm xúc (default là neutral)
            chapter.mood_code = await DetectMoodAsync(content, ct);
            //gọi AI tóm tắt chapter
            chapter.summary = await _openAiModerationService.SummarizeChapterAsync(content, ct);
            //gọi openAI kiểm duyệt 
            var langCode = chapter.story?.language?.lang_code ?? "vi-VN";
            var moderation = await _openAiModerationService.ModerateChapterAsync(chapter.title, content, langCode, ct);
            var aiScoreDecimal = (decimal)Math.Round(moderation.Score, 2, MidpointRounding.AwayFromZero);
            
            var aiViolationsJson = moderation.Violations?.Length > 0
                ? JsonSerializer.Serialize(moderation.Violations, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                : null;

            var timestamp = TimezoneConverter.VietnamNow;

            chapter.updated_at = timestamp;
            chapter.submitted_at = timestamp;

            var shouldReject = moderation.ShouldReject || aiScoreDecimal < 5m;
            var autoApprove = !shouldReject && aiScoreDecimal > 7m;
            var notifyFollowers = false;

            if (shouldReject)
            {
                chapter.status = "rejected";
                chapter.published_at = null;
                await _chapterRepository.UpdateAsync(chapter, ct);

                await UpsertChapterApprovalAsync(chapter, "rejected", aiScoreDecimal, moderation.Explanation, aiViolationsJson, ct);

                var approvalsAfterReject = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
                
                return MapChapter(chapter, approvalsAfterReject, moderation.Violations.Select(v => new
                {
                    v.Word,
                    v.Count,
                    Samples = v.Samples
                }));
            }

            if (autoApprove)
            {
                chapter.status = "published";
                chapter.published_at ??= TimezoneConverter.VietnamNow;
                await _chapterRepository.UpdateAsync(chapter, ct);
                await UpsertChapterApprovalAsync(chapter, "approved", aiScoreDecimal, moderation.Explanation, aiViolationsJson, ct);
                notifyFollowers = true;
            }
            else
            {
                chapter.status = "pending";
                chapter.published_at = null;
                await _chapterRepository.UpdateAsync(chapter, ct);

                await UpsertChapterApprovalAsync(chapter, "pending", aiScoreDecimal, moderation.Explanation, aiViolationsJson, ct);
            }

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);

            if (notifyFollowers)
            {
                var authorName = story.author?.account.username ?? "Tác giả ";
                await _followerNotificationService.NotifyChapterPublishedAsync(
                    story.author_id,
                    authorName,
                    story.story_id,
                    story.title,
                    chapter.chapter_id,
                    chapter.title,
                    (int)chapter.chapter_no,
                    ct);
            }

            return MapChapter(chapter, approvals);
        }

        public async Task<ChapterResponse> WithdrawAsync(Guid authorAccountId, Guid chapterId, CancellationToken ct = default)
        {
            
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả chưa được đăng ký.", 404);

            var chapter = await _chapterRepository.GetByIdAsync(chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            _ = await _storyRepository.GetByIdForAuthorAsync(chapter.story_id, author.account_id, ct)
                ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            if (!string.Equals(chapter.status, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("WithdrawNotAllowed", "Chỉ rút được những chương bị từ chối.", 400);
            }

            chapter.status = "draft";
            chapter.submitted_at = null;
            chapter.published_at = null;
            chapter.updated_at = TimezoneConverter.VietnamNow;
            await _chapterRepository.UpdateAsync(chapter, ct);

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        public async Task<ChapterResponse> UpdateDraftAsync(Guid authorAccountId, Guid storyId, Guid chapterId, ChapterUpdateRequest request, CancellationToken ct = default)
        {
            var author = await _storyRepository.GetAuthorAsync(authorAccountId, ct)
                         ?? throw new AppException("AuthorNotFound", "Hồ sơ tác giả chưa được đăng ký.", 404);

            var story = await _storyRepository.GetByIdForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            var chapter = await _chapterRepository.GetByIdForAuthorAsync(story.story_id, chapterId, author.account_id, ct)
                         ?? throw new AppException("ChapterNotFound", "Không tìm thấy chương.", 404);

            if (!string.Equals(chapter.status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterUpdateNotAllowed", "Chỉ những chương nháp mới có thể sửa trước khi nộp.", 400);
            }

            var updated = false;

            if (request.Title != null)
            {
                var title = request.Title.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    throw new AppException("InvalidChapterTitle", "Tiêu đề không được trống.", 400);
                }

                chapter.title = title;
                updated = true;
            }

            if (request.Content != null)
            {
                var content = request.Content.Trim();
                if (content.Length < MinContentLength)
                {
                    throw new AppException("ChapterContentTooShort", $"Nội dung chương ít nhất {MinContentLength} kí tự.", 400);
                }
                if (content.Length > MaxContentLength)
                {
                    throw new AppException("ChapterContentTooLong", $"Nội dung chương tối đa {MaxContentLength} kí tự.", 400);
                }

                var wordCount = CountWords(content);
                if (wordCount <= 0)
                {
                    throw new AppException("ChapterContentEmpty", "Nội dung chương không được trống.", 400);
                }

                var charCount = content.Length;
                var price = await _chapterPricingService.GetPriceAsync(charCount, ct);
                var previousContentKey = chapter.content_url;
                string? newContentKey = null;
                try
                {
                    newContentKey = await _contentStorage.UploadAsync(story.story_id, chapter.chapter_id, content, ct);

                    var shouldDeleteOld = !string.IsNullOrWhiteSpace(previousContentKey)
                        && !string.Equals(previousContentKey, newContentKey, StringComparison.Ordinal);
                    if (shouldDeleteOld)
                    {
                        await _contentStorage.DeleteAsync(previousContentKey!, ct);
                    }

                    chapter.content_url = newContentKey;
                }
                catch
                {
                    var shouldDeleteNew = !string.IsNullOrWhiteSpace(newContentKey)
                        && (string.IsNullOrWhiteSpace(previousContentKey)
                            || !string.Equals(previousContentKey, newContentKey, StringComparison.Ordinal));
                    if (shouldDeleteNew)
                    {
                        await _contentStorage.DeleteAsync(newContentKey!, ct);
                    }
                    throw;
                }

                chapter.word_count = wordCount;
                chapter.char_count = charCount;
                chapter.dias_price = (uint)price;
                updated = true;
            }

            if (request.AccessType != null)
            {
                var normalizedAccessType = NormalizeOptionalChapterAccessType(request.AccessType)
                                           ?? throw new AppException("InvalidAccessType", "Loại truy cập là bắt buộc khi được cung cấp.", 400);

                var canLockChapters = CanAuthorLockChapters(author, story);
                if (string.Equals(normalizedAccessType, "dias", StringComparison.OrdinalIgnoreCase) && !canLockChapters)
                {
                    throw new AppException("AccessTypeNotAllowed", "Chỉ những tác giả Sponsored mới có thể đăng chương trả phí.", 400);
                }

                if (!string.Equals(chapter.access_type, normalizedAccessType, StringComparison.OrdinalIgnoreCase))
                {
                    chapter.access_type = normalizedAccessType;
                    updated = true;
                }
            }

            if (!updated)
            {
                throw new AppException("NoChanges", "Không có thay đổi nào được cung cấp.", 400);
            }

            chapter.updated_at = TimezoneConverter.VietnamNow;
            await _chapterRepository.UpdateAsync(chapter, ct);

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            return MapChapter(chapter, approvals);
        }

        private static IReadOnlyList<string>? NormalizeChapterStatuses(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var normalized = status.Trim();
            if (!AuthorChapterAllowedStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Trạng thái '{status}' không được hỗ trợ. Các giá trị cho phép là: {string.Join(", ", AuthorChapterAllowedStatuses)}.", 400);
            }

            return new[] { normalized.ToLowerInvariant() };
        }

        private static string? NormalizeOptionalChapterAccessType(string? accessType)
        {
            if (accessType == null)
            {
                return null;
            }

            var normalized = accessType.Trim().ToLowerInvariant();
            if (normalized.Length == 0)
            {
                throw new AppException("InvalidAccessType", "Loại truy cập phải là 'free' hoặc 'dias'.", 400);
            }

            if (!ChapterAccessTypes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidAccessType", $"Loại truy cập '{accessType}' không được hỗ trợ. Các giá trị cho phép là: {string.Join(", ", ChapterAccessTypes)}.", 400);
            }

            return normalized;
        }

        private static bool CanAuthorLockChapters(author author, story story)
        {
            if (story.is_premium)
            {
                return true;
            }

            var rankName = author.rank?.rank_name;
            return !string.IsNullOrWhiteSpace(rankName) &&
                   !string.Equals(rankName, "casual", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveAccessTypeForCreate(string? requestedAccessType, bool canLockChapters, bool storyIsPremium)
        {
            var defaultAccess = storyIsPremium ? "dias" : "free";
            if (requestedAccessType == null)
            {
                return canLockChapters ? defaultAccess : "free";
            }

            if (string.Equals(requestedAccessType, "dias", StringComparison.OrdinalIgnoreCase) && !canLockChapters)
            {
                throw new AppException("AccessTypeNotAllowed", "Chỉ những tác giả Sponsored mới có thể đăng chương trả phí.", 400);
            }

            return requestedAccessType;
        }

        private ChapterResponse MapChapter(chapter chapter, IEnumerable<content_approve> approvals, object? immediateViolations = null)
        {
            var story = chapter.story;
            var approval = approvals
                .Where(a => string.Equals(a.approve_type, "chapter", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.created_at)
                .FirstOrDefault();

            var moderatorStatus = approval?.moderator_id.HasValue == true ? approval.status : null;
            var moderatorNote = approval?.moderator_id.HasValue == true ? approval.moderator_feedback : null;

            object? violations = immediateViolations;
            if (violations == null && !string.IsNullOrWhiteSpace(approval?.ai_violations))
            {
                try
                {
                    violations = JsonSerializer.Deserialize<object>(approval.ai_violations);
                }
                catch
                {
                    // Ignore
                }
            }

            return new ChapterResponse
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                Summary = chapter.summary,
                WordCount = chapter.word_count,
                CharCount = chapter.char_count,
                LanguageCode = story.language.lang_code,
                LanguageName = story.language.lang_name,
                PriceDias = (int)chapter.dias_price,
                AccessType = chapter.access_type,
                Status = chapter.status,
                AiScore = approval?.ai_score,
                AiFeedback = approval?.ai_feedback,
                AiViolations = violations,
                AiResult = ResolveAiDecision(approval),
                ModeratorStatus = moderatorStatus,
                ModeratorNote = moderatorNote,
                ContentPath = chapter.content_url,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at,
                SubmittedAt = chapter.submitted_at,
                PublishedAt = chapter.published_at,
                Mood = MapMood(chapter),
                Voices = MapVoices(chapter)
            };
        }

        private static VoiceChapterVoiceResponse[] MapVoices(chapter chapter)
        {
            if (chapter.chapter_voices == null || chapter.chapter_voices.Count == 0)
            {
                return Array.Empty<VoiceChapterVoiceResponse>();
            }

            return chapter.chapter_voices
                .OrderBy(v => v.voice?.voice_name)
                .Select(v => new VoiceChapterVoiceResponse
                {
                    VoiceId = v.voice_id,
                    VoiceName = v.voice?.voice_name ?? string.Empty,
                    VoiceCode = v.voice?.voice_code ?? string.Empty,
                    Status = v.status,
                    AudioUrl = v.storage_path,
                    RequestedAt = v.requested_at,
                    CompletedAt = v.completed_at,
                    CharCost = v.char_cost,
                    ErrorMessage = v.error_message
                })
                .ToArray();
        }

        private static ChapterListItemResponse MapChapterListItem(chapter chapter)
        {
            var language = chapter.story?.language ?? throw new InvalidOperationException("Chapter story language navigation was not loaded.");
            var approval = chapter.content_approves?
                .OrderByDescending(a => a.created_at)
                .FirstOrDefault();
            var moderatorStatus = approval?.moderator_id.HasValue == true ? approval.status : null;
            var moderatorNote = approval?.moderator_id.HasValue == true ? approval.moderator_feedback : null;

            return new ChapterListItemResponse
            {
                ChapterId = chapter.chapter_id,
                ChapterNo = (int)chapter.chapter_no,
                Title = chapter.title,
                WordCount = chapter.word_count,
                CharCount = chapter.char_count,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                PriceDias = (int)chapter.dias_price,
                Status = chapter.status,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at,
                SubmittedAt = chapter.submitted_at,
                PublishedAt = chapter.published_at,
                AiScore = approval?.ai_score,
                AiResult = ResolveAiDecision(approval),
                AiFeedback = approval?.ai_feedback,
                ModeratorStatus = moderatorStatus,
                ModeratorNote = moderatorNote,
                Mood = MapMood(chapter)
            };
        }

        private static string? ResolveAiDecision(content_approve? approval)
        {
            if (approval == null || approval.ai_score is not decimal score)
            {
                return null;
            }

            if (score < 5m)
            {
                return "rejected";
            }

            if (score > 7m)
            {
                return "approved";
            }

            return "flagged";
        }

        private async Task<content_approve> UpsertChapterApprovalAsync(chapter chapter, string status, decimal aiScore, string? aiNote, string? aiViolationsJson, CancellationToken ct)
        {
            var approval = await _chapterRepository.GetContentApprovalForChapterAsync(chapter.chapter_id, ct);
            var timestamp = TimezoneConverter.VietnamNow;

            if (approval == null)
            {
                approval = new content_approve
                {
                    approve_type = "chapter",
                    story_id = chapter.story_id,
                    chapter_id = chapter.chapter_id,
                    status = status,
                    ai_score = aiScore,
                    ai_feedback = aiNote,
                    ai_violations = aiViolationsJson,
                    moderator_feedback = null,
                    moderator_id = null,
                    created_at = timestamp
                };

                await _chapterRepository.AddContentApproveAsync(approval, ct);
            }
            else
            {
                approval.status = status;
                approval.ai_score = aiScore;
                approval.ai_feedback = aiNote;
                approval.ai_violations = aiViolationsJson;
                approval.moderator_feedback = null;
                approval.moderator_id = null;
                approval.created_at = timestamp;

                await _chapterRepository.SaveChangesAsync(ct);
            }

            return approval;
        }

        private static int CountWords(string content)
        {
            var matches = Regex.Matches(content, @"[\p{L}\p{N}']+");
            return matches.Count;
        }

        private static ChapterMoodResponse? MapMood(chapter chapter)
        {
            if (chapter.mood != null)
            {
                return new ChapterMoodResponse
                {
                    Code = chapter.mood.mood_code,
                    Name = chapter.mood.mood_name
                };
            }

            if (!string.IsNullOrWhiteSpace(chapter.mood_code))
            {
                return new ChapterMoodResponse
                {
                    Code = chapter.mood_code,
                    Name = chapter.mood_code
                };
            }

            return null;
        }

        private async Task<string> DetectMoodAsync(string content, CancellationToken ct)
        {
            if (_openAiModerationService is OpenAiService concrete)
            {
                return await concrete.DetectMoodAsync(content, ct);
            }

            return "neutral";
        }
    }
}
using Contract.DTOs.Request.Story;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Service.Helpers;
using Contract.DTOs.Response.Story;
using System.Text.Json;

namespace Service.Services
{
    public class AuthorStoryService : IAuthorStoryService
    {
        private readonly IAuthorStoryRepository _storyRepository;
        private readonly IImageUploader _imageUploader;
        private readonly IOpenAiImageService _openAiImageService;
        private readonly IOpenAiModerationService _openAiModerationService;
        private readonly IFollowerNotificationService _followerNotificationService;
        private readonly IProfileRepository? _profileRepository;

        //cover mode cho phép 
        private static readonly string[] AllowedCoverModes = { "upload", "generate" };
        //status story cho phép
        private static readonly string[] AuthorListAllowedStatuses = { "draft", "pending", "rejected", "published", "completed", "hidden", "removed" };
        private static readonly HashSet<string> AllowedLengthPlans = new(StringComparer.OrdinalIgnoreCase)
        {
            "novel", "short", "super_short"
        };

        //set đk complete từng length plan
        private static readonly Dictionary<string, int> CompletionChapterRequirements = new(StringComparer.OrdinalIgnoreCase)
        {
            ["super_short"] = 1,
            ["short"] = 6,
            ["novel"] = 21
        };

        public AuthorStoryService(
            IAuthorStoryRepository storyRepository,
            IImageUploader imageUploader,
            IOpenAiImageService openAiImageService,
            IOpenAiModerationService openAiModerationService,
            IFollowerNotificationService followerNotificationService,
            IProfileRepository? profileRepository = null)
        {
            _storyRepository = storyRepository;
            _imageUploader = imageUploader;
            _openAiImageService = openAiImageService;
            _openAiModerationService = openAiModerationService;
            _followerNotificationService = followerNotificationService;
            _profileRepository = profileRepository;
        }

        public async Task<StoryResponse> CreateAsync(Guid authorAccountId, StoryCreateRequest request, CancellationToken ct = default)
        {

            //check coi user có profile author ko 
            var author = await RequireAuthorAsync(authorAccountId, ct);


            //check coi có bị restrict ko 
            await AccountRestrictionHelper.EnsureCanPublishAsync(author.account, _profileRepository, ct);

            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Tài khoản tác giả của bạn đang bị hạn chế.", 403);
            }

            var lastRejectedAt = await _storyRepository.GetLastAuthorStoryRejectedAtAsync(author.account_id, ct);
            //if (lastRejectedAt.HasValue && lastRejectedAt.Value > TimezoneConverter.VietnamNow.AddHours(-24))
            //{
            //    throw new AppException("StoryCreationCooldown", "You must wait 24 hours after a rejection before creating a new story.", 400, new
            //    {
            //        availableAtUtc = lastRejectedAt.Value.AddHours(24)
            //    });
            //}


            //1 author chỉ đc có 1 truyện pending 1 thời điểm
            if (await _storyRepository.AuthorHasPendingStoryAsync(author.account_id, null, ct))
            {
                throw new AppException("PendingStoryLimit", "Bạn đã có một truyện đang chờ kiểm duyệt.", 400);
            }

            //1 author chỉ đc có 1 truyện published 1 thời điểm
            if (await _storyRepository.AuthorHasUncompletedPublishedStoryAsync(author.account_id, ct))
            {
                throw new AppException("PublishedStoryIncomplete", "Vui lòng hoàn thành truyện đã xuất bản của bạn trước khi tạo truyện mới.", 400);
            }

            //cover mode mà ko giống khai báo đầu service thì trả lỗi
            if (!AllowedCoverModes.Contains(request.CoverMode, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidCoverMode", "Chế độ ảnh bìa phải là 'upload' hoặc 'generate'.", 400);
            }

            //lấy list tag riêng biệt để check tag 
            var tagIds = request.TagIds.Distinct().ToArray();
            var outline = (request.Outline ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outline))
            {
                throw new AppException("OutlineRequired", "Cốt truyện là bắt buộc.", 400);
            }

            var lengthPlan = NormalizeLengthPlan(request.LengthPlan);

            var languageCode = (request.LanguageCode ?? string.Empty).Trim();
            var language = await _storyRepository.GetLanguageByCodeAsync(languageCode, ct)
                          ?? throw new AppException("LanguageNotSupported", $"Ngôn ngữ '{languageCode}' không được hỗ trợ.", 400);

            //tách tag riêng biệt xong thì get by id để check từng tag coi hợp lệ ko 
            var tags = await _storyRepository.GetTagsByIdsAsync(tagIds, ct);
            if (tags.Count != tagIds.Length)
            {
                throw new AppException("InvalidTag", "Một hoặc nhiều thẻ không tồn tại.", 400);
            }

            var coverUrl = await ResolveCoverUrlAsync(request.CoverMode, request.CoverFile, request.CoverPrompt, ct);
            var story = new story
            {
                author_id = author.account_id,
                title = request.Title.Trim(),
                language_id = language.lang_id,
                desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                outline = outline,
                length_plan = lengthPlan,
                cover_url = coverUrl,
                status = "draft",
                is_premium = false
            };
            story.language = language;

            await _storyRepository.CreateAsync(story, tagIds, ct);


            //create xong thì get by id liền bỏ vô response
            var saved = await _storyRepository.GetByIdForAuthorAsync(story.story_id, author.account_id, ct)
                        ?? throw new InvalidOperationException("Failed to load story after creation.");
            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(saved.story_id, ct);

            return MapStory(saved, approvals);
        }

        public async Task<StoryResponse> SubmitForReviewAsync(Guid authorAccountId, Guid storyId, StorySubmitRequest request, CancellationToken ct = default)
        {
            //check status author 
            var author = await RequireAuthorAsync(authorAccountId, ct);
            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Tài khoản tác giả của bạn đang bị hạn chế.", 403);
            }


            //check story có tồn tại ko và check các validation 
            var story = await _storyRepository.GetByIdForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            if (story.status == "pending")
            {
                throw new AppException("StoryPending", "Truyện đang chờ kiểm duyệt.", 400);
            }

            if (story.status == "published")
            {
                throw new AppException("StoryPublished", "Truyện đã xuất bản không thể gửi lại.", 400);
            }

            if (story.status == "rejected")
            {
                throw new AppException("StoryRejectedLocked", "Truyện bị từ chối không thể gửi lại. Vui lòng tạo truyện mới.", 400);
            }

            if (await _storyRepository.AuthorHasPendingStoryAsync(author.account_id, story.story_id, ct))
            {
                throw new AppException("PendingStoryLimit", "Bạn đã có một truyện khác đang chờ kiểm duyệt.", 400);
            }

            if (await _storyRepository.AuthorHasUncompletedPublishedStoryAsync(author.account_id, ct))
            {
                throw new AppException("PublishedStoryIncomplete", "Vui lòng hoàn thành truyện đã xuất bản của bạn trước khi gửi truyện mới.", 400);
            }

            var langCode = story.language?.lang_code ?? "vi-VN";

            //gọi bên OpenAIService để request title và desc cho AI 
            var aiResult = await _openAiModerationService.ModerateStoryAsync(story.title, story.desc, story.outline, langCode, ct);
            //làm tròn điểm AI 
            var aiScore = (decimal)Math.Round(aiResult.Score, 2, MidpointRounding.AwayFromZero);

            var aiViolationsJson = aiResult.Violations?.Count > 0
                ? JsonSerializer.Serialize(aiResult.Violations, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                : null;

            //explanation của AI 
            var aiNote = aiResult.Explanation;
            var now = TimezoneConverter.VietnamNow;

            story.updated_at = now;
            var initialStatus = story.status;
            var notifyFollowers = false;


            //nếu AI trả reject/điểm <5 thì: 
            if (aiResult.ShouldReject || aiScore < 5m)
            {
                story.status = "rejected";
                story.published_at = null;
                await _storyRepository.UpdateAsync(story, ct);

                await UpsertStoryApprovalAsync(story.story_id, "rejected", aiScore, aiNote, aiViolationsJson, ct);
                
                var savedAfterReject = await _storyRepository.GetByIdForAuthorAsync(story.story_id, author.account_id, ct)
                            ?? throw new InvalidOperationException("Failed to load story after rejection.");
                var approvalsAfterReject = await _storyRepository.GetContentApprovalsForStoryAsync(savedAfterReject.story_id, ct);
                
                return MapStory(savedAfterReject, approvalsAfterReject, aiResult.Violations.Select(v => new
                {
                    v.Word,
                    v.Count,
                    Samples = v.Samples,
                    v.Penalty
                }));
            }
            //nếu >7 thì giống ở trên nhưng khác cái là published 
            if (aiScore > 7m)
            {
                story.status = "published";
                story.published_at ??= TimezoneConverter.VietnamNow;

                story.is_premium = false;
                if (!string.Equals(initialStatus, "published", StringComparison.OrdinalIgnoreCase))
                {
                    author.total_story += 1;
                }
                await _storyRepository.UpdateAsync(story, ct);

                await UpsertStoryApprovalAsync(story.story_id, "approved", aiScore, aiNote, aiViolationsJson, ct);
                notifyFollowers = !string.Equals(initialStatus, "published", StringComparison.OrdinalIgnoreCase);
            }
            else //nếu từ >=5 và <7 thì pending đợi cmod check
            {
                story.status = "pending";
                story.published_at = null;
                await _storyRepository.UpdateAsync(story, ct);

                await UpsertStoryApprovalAsync(story.story_id, "pending", aiScore, aiNote, aiViolationsJson, ct);
            }

            //kiểm duyệt xong hết thì gọi repo lấy full thông tin story *sau khi đc moderation để đc submit
            var saved = await _storyRepository.GetByIdForAuthorAsync(story.story_id, author.account_id, ct)
                        ?? throw new InvalidOperationException("Failed to load story after submission.");
            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(saved.story_id, ct);


            //bắn follow cho tất cả reader follow author này 
            if (notifyFollowers)
            {
                var authorName = author.account.username;
                await _followerNotificationService.NotifyStoryPublishedAsync(author.account_id, authorName, story.story_id, story.title, ct);
            }

            return MapStory(saved, approvals);
        }

        public async Task<IReadOnlyList<StoryListItemResponse>> GetAllAsync(Guid authorAccountId, string? status, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            var filterStatuses = NormalizeAuthorStatuses(status);
            var stories = await _storyRepository.GetAllByAuthorAsync(author.account_id, filterStatuses, ct);

            var responses = new List<StoryListItemResponse>(stories.Count);
            foreach (var story in stories)
            {
                responses.Add(MapStoryListItem(story));
            }

            return responses;
        }

        public async Task<StoryResponse> GetByIdAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            var story = await _storyRepository.GetByIdForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(story, approvals);
        }

        public async Task<StoryResponse> CompleteAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);

            //đảm bảo story id này tồn tại 
            var story = await _storyRepository.GetByIdForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            //chỉ complete đc truyện đã thông qua duyệt và trong trạng thái published 
            if (story.status != "published")
            {
                throw new AppException("StoryNotPublished", "Chỉ hoàn thành được truyện nào đã phát hành.", 400);
            }

            if (await _storyRepository.HasDraftChaptersAsync(story.story_id, ct))
            {
                throw new AppException("StoryHasDraftChapters", "Không thể hoàn thành truyện khi còn chương nháp.", 400);
            }

            //đếm chapter trong story này để cbi validation length plan ở dưới 
            var chapterCount = await _storyRepository.GetNonDraftChapterCountAsync(story.story_id, ct);
            var plan = story.length_plan ?? "super_short";

            //check chapter của story đáp ứng đc mốc định ra ở đầu service k (1,6,21)
            if (!CompletionChapterRequirements.TryGetValue(plan, out var requiredChapters))
            {
                requiredChapters = 1;
            } //trả lỗi khi ko đủ số chap yêu cầu 
            if (chapterCount < requiredChapters)
            {
                throw new AppException("StoryInsufficientChapters", $"Truyện với kế hoạch độ dài '{plan}' yêu cầu ít nhất {requiredChapters} chương trước khi hoàn thành.", 400);
            }
            
            var publishedAt = story.published_at ?? await _storyRepository.GetStoryPublishedAtAsync(story.story_id, ct);
            if (!publishedAt.HasValue)
            {
                publishedAt = story.updated_at;
                story.published_at = publishedAt;
            }

            //var earliestCompletion = publishedAt.Value.AddDays(1);
            //if (TimezoneConverter.VietnamNow < earliestCompletion)
            //{
            //    throw new AppException("StoryCompletionCooldown", "Story must be published for at least 30 days before completion.", 400, new
            //    {
            //        availableAtUtc = earliestCompletion
            //    });
            //}


            //update status từ published -> completed 
            story.status = "completed";
            story.updated_at = TimezoneConverter.VietnamNow;

            await _storyRepository.UpdateAsync(story, ct);

            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(story, approvals);
        }

        public async Task<StoryResponse> UpdateDraftAsync(Guid authorAccountId, Guid storyId, StoryUpdateRequest request, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Tài khoản tác giả của bạn đang bị hạn chế.", 403);
            }

            var story = await _storyRepository.GetByIdForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Không tìm thấy truyện.", 404);

            //check coi có đang draft ko (đúng như tên method chỉ update đc story đang draft)
            if (!string.Equals(story.status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryUpdateNotAllowed", "Chỉ những truyện nháp mới có thể chỉnh sửa trước khi gửi.", 400);
            }

            var updated = false;

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                story.title = request.Title.Trim();
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(request.LanguageCode))
            {
                var langCode = request.LanguageCode.Trim();
                var language = await _storyRepository.GetLanguageByCodeAsync(langCode, ct)
                              ?? throw new AppException("LanguageNotSupported", $"Ngôn ngữ '{langCode}' không được hỗ trợ.", 400);

                if (story.language_id != language.lang_id)
                {
                    story.language_id = language.lang_id;
                    story.language = language;
                    updated = true;
                }
            }

            if (request.Description != null)
            {
                story.desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                updated = true;
            }

            if (request.Outline != null)
            {
                var outline = request.Outline.Trim();
                if (string.IsNullOrWhiteSpace(outline))
                {
                    throw new AppException("OutlineRequired", "Cốt truyện không được để trống.", 400);
                }
                story.outline = outline;
                updated = true;
            }

            if (request.LengthPlan != null)
            {
                story.length_plan = NormalizeLengthPlan(request.LengthPlan);
                updated = true;
            }

            if (request.TagIds is { Count: > 0 })
            {
                var tagIds = request.TagIds.Distinct().ToArray();
                var tags = await _storyRepository.GetTagsByIdsAsync(tagIds, ct);
                if (tags.Count != tagIds.Length)
                {
                    throw new AppException("InvalidTag", "Một hoặc nhiều thẻ không tồn tại.", 400);
                }

                await _storyRepository.ReplaceStoryTagsAsync(story.story_id, tagIds, ct);
                updated = true;
            }
            //dùng chung form request lúc create NHƯNG không cho dùng cover mode generate lúc update 
            if (!string.IsNullOrWhiteSpace(request.CoverMode))
            {
                var coverMode = request.CoverMode.Trim().ToLowerInvariant();
                if (!string.Equals(coverMode, "upload", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AppException("CoverRegenerationNotAllowed", "Tính năng tạo ảnh bìa AI không khả dụng khi chỉnh sửa bản nháp hiện có. Vui lòng tải lên ảnh mới thay thế.", 400);
                }

                var coverUrl = await ResolveCoverUrlAsync("upload", request.CoverFile, null, ct);
                story.cover_url = coverUrl;
                updated = true;
            }

            if (!updated)
            {
                throw new AppException("NoChanges", "Không có thay đổi nào được cung cấp.", 400);
            }

            story.updated_at = TimezoneConverter.VietnamNow;
            await _storyRepository.UpdateAsync(story, ct);

            var refreshed = await _storyRepository.GetByIdForAuthorAsync(story.story_id, author.account_id, ct) ?? story;
            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(refreshed, approvals);
        }

        //method này để xử lí up ảnh lúc gen ra ảnh AI hoặc upload 
        private async Task<string?> ResolveCoverUrlAsync(string coverMode, IFormFile? coverFile, string? coverPrompt, CancellationToken ct)
        {
            var mode = coverMode.ToLowerInvariant();
            switch (mode)
            {
                case "upload":
                    //đã chọn upload thì phải có file trong form 
                    if (coverFile == null || coverFile.Length == 0)
                    {
                        throw new AppException("CoverRequired", "File ảnh bìa phải được cung cấp khi chế độ là 'upload'.", 400);
                    }

                    await using (var stream = coverFile.OpenReadStream())
                    {
                        //gọi cloudinary đẩy lên 
                        return await _imageUploader.UploadStoryCoverAsync(stream, coverFile.FileName, ct);
                    }

                case "generate":
                    if (string.IsNullOrWhiteSpace(coverPrompt))
                    {
                        throw new AppException("CoverPromptRequired", "Prompt ảnh bìa phải được cung cấp khi chế độ là 'generate'.", 400);
                    }

                    var image = await _openAiImageService.GenerateCoverAsync(coverPrompt, ct);
                    try
                    {
                        return await _imageUploader.UploadStoryCoverAsync(image.Data, image.FileName, ct);
                    }
                    finally
                    {
                        image.Data.Dispose();
                    }

                default:
                    throw new AppException("InvalidCoverMode", "Chế độ ảnh bìa phải là 'upload' hoặc 'generate'.", 400);
            }
        }

        private static IReadOnlyList<string>? NormalizeAuthorStatuses(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var normalized = status.Trim();
            if (!AuthorListAllowedStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidStatus", $"Trạng thái '{status}' không được hỗ trợ. Các giá trị cho phép là: {string.Join(", ", AuthorListAllowedStatuses)}.", 400);
            }

            return new[] { normalized.ToLowerInvariant() };
        }

        private async Task<author> RequireAuthorAsync(Guid accountId, CancellationToken ct)
        {
            var author = await _storyRepository.GetAuthorAsync(accountId, ct);
            if (author == null)
            {
                throw new AppException("AuthorNotFound", "Hồ sơ tác giả chưa được đăng ký.", 404);
            }
            return author;
        }

        private async Task<content_approve> UpsertStoryApprovalAsync(Guid storyId, string status, decimal aiScore, string? aiNote, string? aiViolationsJson, CancellationToken ct)
        {
            var approval = await _storyRepository.GetContentApprovalForStoryAsync(storyId, ct);
            var timestamp = TimezoneConverter.VietnamNow;

            if (approval == null)
            {
                approval = new content_approve
                {
                    approve_type = "story",
                    story_id = storyId,
                    status = status,
                    ai_score = aiScore,
                    ai_feedback = aiNote,
                    ai_violations = aiViolationsJson,
                    moderator_feedback = null,
                    moderator_id = null,
                    created_at = timestamp
                };

                await _storyRepository.AddContentApproveAsync(approval, ct);
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

                await _storyRepository.SaveChangesAsync(ct);
            }

            return approval;
        }


        //map hết info từ nhiều bảng khác nhau vô response)
        private StoryResponse MapStory(story story, IEnumerable<content_approve> approvals, object? immediateViolations = null)
        {
            var language = story.language;
            var tags = story.story_tags?
                .Where(st => st.tag != null)
                .Select(st => new StoryTagResponse
                {
                    TagId = st.tag_id,
                    TagName = st.tag.tag_name
                })
                .OrderBy(t => t.TagName, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<StoryTagResponse>();

            var approval = approvals
                .Where(a => string.Equals(a.approve_type, "story", StringComparison.OrdinalIgnoreCase))
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
                    // Fail silently on corrupt JSON
                }
            }

            return new StoryResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                Description = story.desc,
                Status = story.status,
                IsPremium = story.is_premium,
                CoverUrl = story.cover_url,
                Outline = story.outline,
                LengthPlan = story.length_plan,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                PublishedAt = story.published_at,
                Tags = tags,
                AiScore = approval?.ai_score,
                AiResult = ResolveAiDecision(approval),
                AiFeedback = approval?.ai_feedback,
                AiViolations = violations,
                ModeratorStatus = moderatorStatus,
                ModeratorNote = moderatorNote
            };
        }

        private static StoryListItemResponse MapStoryListItem(story story)
        {
            var language = story.language ?? throw new InvalidOperationException("Story language navigation was not loaded.");

            var tags = story.story_tags?
                .Where(st => st.tag != null)
                .Select(st => new StoryTagResponse
                {
                    TagId = st.tag_id,
                    TagName = st.tag.tag_name
                })
                .OrderBy(t => t.TagName, StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<StoryTagResponse>();

            var approval = story.content_approves?
                .Where(a => string.Equals(a.approve_type, "story", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.created_at)
                .FirstOrDefault();

            var moderatorStatus = approval?.moderator_id.HasValue == true ? approval.status : null;
            var moderatorNote = approval?.moderator_id.HasValue == true ? approval.moderator_feedback : null;

            return new StoryListItemResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                Status = story.status,
                IsPremium = story.is_premium,
                CoverUrl = story.cover_url,
                LengthPlan = story.length_plan,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                PublishedAt = story.published_at,
                Tags = tags,
                AiScore = approval?.ai_score,
                AiResult = ResolveAiDecision(approval),
                AiFeedback = approval?.ai_feedback,
                ModeratorStatus = moderatorStatus,
                ModeratorNote = moderatorNote
            };
        }

        private static string? ResolveAiDecision(content_approve? approval)
        {
            if (approval == null)
            {
                return null;
            }

            if (approval.ai_score is not decimal score)
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

        private static string NormalizeLengthPlan(string? plan)
        {
            if (string.IsNullOrWhiteSpace(plan))
            {
                throw new AppException("LengthPlanRequired", "Không được bỏ trống độ dài dự kiến.", 400);
            }

            var normalized = plan.Trim().ToLowerInvariant();
            if (!AllowedLengthPlans.Contains(normalized))
            {
                throw new AppException("InvalidLengthPlan", "Kế hoạch độ dài phải là novel, short, hoặc super_short.", 400);
            }
            return normalized;
        }
    }
}
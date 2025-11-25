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

        private static readonly string[] AllowedCoverModes = { "upload", "generate" };
        private static readonly string[] AuthorListAllowedStatuses = { "draft", "pending", "rejected", "published", "completed", "hidden", "removed" };
        private static readonly HashSet<string> AllowedLengthPlans = new(StringComparer.OrdinalIgnoreCase)
        {
            "novel", "short", "super_short"
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
            var author = await RequireAuthorAsync(authorAccountId, ct);

            await AccountRestrictionHelper.EnsureCanPublishAsync(author.account, _profileRepository, ct);

            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Your author account is restricted.", 403);
            }

            var lastRejectedAt = await _storyRepository.GetLastAuthorStoryRejectedAtAsync(author.account_id, ct);
            if (lastRejectedAt.HasValue && lastRejectedAt.Value > TimezoneConverter.VietnamNow.AddHours(-24))
            {
                throw new AppException("StoryCreationCooldown", "You must wait 24 hours after a rejection before creating a new story.", 400, new
                {
                    availableAtUtc = lastRejectedAt.Value.AddHours(24)
                });
            }

            if (await _storyRepository.AuthorHasPendingStoryAsync(author.account_id, null, ct))
            {
                throw new AppException("PendingStoryLimit", "You already have a story waiting for moderation.", 400);
            }

            if (await _storyRepository.AuthorHasUncompletedPublishedStoryAsync(author.account_id, ct))
            {
                throw new AppException("PublishedStoryIncomplete", "Please complete your published story before creating a new one.", 400);
            }

            if (!AllowedCoverModes.Contains(request.CoverMode, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidCoverMode", "CoverMode must be either 'upload' or 'generate'.", 400);
            }

            var tagIds = request.TagIds.Distinct().ToArray();
            var outline = (request.Outline ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outline))
            {
                throw new AppException("OutlineRequired", "Story outline is required.", 400);
            }

            var lengthPlan = NormalizeLengthPlan(request.LengthPlan);

            var tags = await _storyRepository.GetTagsByIdsAsync(tagIds, ct);
            if (tags.Count != tagIds.Length)
            {
                throw new AppException("InvalidTag", "One or more tags do not exist.", 400);
            }

            var coverUrl = await ResolveCoverUrlAsync(request.CoverMode, request.CoverFile, request.CoverPrompt, ct);
            var isPremium = author.rank is not null &&
                            !string.Equals(author.rank.rank_name, "Casual", StringComparison.OrdinalIgnoreCase);

            var story = new story
            {
                author_id = author.account_id,
                title = request.Title.Trim(),
                desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                outline = outline,
                length_plan = lengthPlan,
                cover_url = coverUrl,
                status = "draft",
                is_premium = isPremium
            };

            await _storyRepository.AddStoryAsync(story, tagIds, ct);

            var saved = await _storyRepository.GetStoryForAuthorAsync(story.story_id, author.account_id, ct)
                        ?? throw new InvalidOperationException("Failed to load story after creation.");
            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(saved.story_id, ct);

            return MapStory(saved, approvals);
        }

        public async Task<StoryResponse> SubmitForReviewAsync(Guid authorAccountId, Guid storyId, StorySubmitRequest request, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Your author account is restricted.", 403);
            }

            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            if (story.status == "pending")
            {
                throw new AppException("StoryPending", "Story is already pending moderation.", 400);
            }

            if (story.status == "published")
            {
                throw new AppException("StoryPublished", "Published stories cannot be resubmitted.", 400);
            }

            if (story.status == "rejected")
            {
                throw new AppException("StoryRejectedLocked", "Rejected stories cannot be resubmitted. Please create a new story.", 400);
            }

            if (await _storyRepository.AuthorHasPendingStoryAsync(author.account_id, story.story_id, ct))
            {
                throw new AppException("PendingStoryLimit", "You already have another story waiting for moderation.", 400);
            }

            if (await _storyRepository.AuthorHasUncompletedPublishedStoryAsync(author.account_id, ct))
            {
                throw new AppException("PublishedStoryIncomplete", "Please complete your published story before submitting a new one.", 400);
            }

            var aiResult = await _openAiModerationService.ModerateStoryAsync(story.title, story.desc, ct);
            var aiScore = (decimal)Math.Round(aiResult.Score, 2, MidpointRounding.AwayFromZero);
            var aiNote = aiResult.Explanation;
            var now = TimezoneConverter.VietnamNow;

            story.updated_at = now;
            var initialStatus = story.status;
            var notifyFollowers = false;

            if (aiResult.ShouldReject || aiScore < 5m)
            {
                story.status = "rejected";
                story.published_at = null;
                await _storyRepository.UpdateStoryAsync(story, ct);

                var rejectionApproval = await UpsertStoryApprovalAsync(story.story_id, "rejected", aiScore, aiNote, ct);

                throw new AppException("StoryRejectedByAi", "Story was rejected by automated moderation.", 400, new
                {
                    reviewId = rejectionApproval.review_id,
                    score = Math.Round(aiResult.Score, 2),
                    sanitizedContent = aiResult.SanitizedContent,
                    explanation = aiResult.Explanation,
                    violations = aiResult.Violations.Select(v => new
                    {
                        v.Word,
                        v.Count,
                        Samples = v.Samples
                    })
                });
            }

            if (aiScore >= 7m)
            {
                story.status = "published";
                story.published_at ??= TimezoneConverter.VietnamNow;

                var authorRankName = author.rank?.rank_name;
                story.is_premium = !string.IsNullOrWhiteSpace(authorRankName) &&
                                   !string.Equals(authorRankName, "Casual", StringComparison.OrdinalIgnoreCase);
                await _storyRepository.UpdateStoryAsync(story, ct);

                await UpsertStoryApprovalAsync(story.story_id, "approved", aiScore, aiNote, ct);
                notifyFollowers = !string.Equals(initialStatus, "published", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                story.status = "pending";
                story.published_at = null;
                await _storyRepository.UpdateStoryAsync(story, ct);

                await UpsertStoryApprovalAsync(story.story_id, "pending", aiScore, aiNote, ct);
            }

            var saved = await _storyRepository.GetStoryForAuthorAsync(story.story_id, author.account_id, ct)
                        ?? throw new InvalidOperationException("Failed to load story after submission.");
            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(saved.story_id, ct);

            if (notifyFollowers)
            {
                var authorName = author.account.username;
                await _followerNotificationService.NotifyStoryPublishedAsync(author.account_id, authorName, story.story_id, story.title, ct);
            }

            return MapStory(saved, approvals);
        }

        public async Task<IReadOnlyList<StoryListItemResponse>> ListAsync(Guid authorAccountId, string? status, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            var filterStatuses = NormalizeAuthorStatuses(status);
            var stories = await _storyRepository.GetStoriesByAuthorAsync(author.account_id, filterStatuses, ct);

            var responses = new List<StoryListItemResponse>(stories.Count);
            foreach (var story in stories)
            {
                responses.Add(MapStoryListItem(story));
            }

            return responses;
        }

        public async Task<StoryResponse> GetAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(story, approvals);
        }

        public async Task<StoryResponse> CompleteAsync(Guid authorAccountId, Guid storyId, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            if (story.status != "published")
            {
                throw new AppException("StoryNotPublished", "Only published stories can be marked as completed.", 400);
            }

            var chapterCount = await _storyRepository.GetChapterCountAsync(story.story_id, ct);
            if (chapterCount < 1)
            {
                throw new AppException("StoryInsufficientChapters", "Story needs at least one chapter before completion.", 400);
            }

            var publishedAt = story.published_at ?? await _storyRepository.GetStoryPublishedAtAsync(story.story_id, ct);
            if (!publishedAt.HasValue)
            {
                publishedAt = story.updated_at;
                story.published_at = publishedAt;
            }

            var earliestCompletion = publishedAt.Value.AddDays(1);
            if (TimezoneConverter.VietnamNow < earliestCompletion)
            {
                throw new AppException("StoryCompletionCooldown", "Story must be published for at least 30 days before completion.", 400, new
                {
                    availableAtUtc = earliestCompletion
                });
            }

            story.status = "completed";
            story.updated_at = TimezoneConverter.VietnamNow;

            await _storyRepository.UpdateStoryAsync(story, ct);

            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(story, approvals);
        }

        public async Task<StoryResponse> UpdateDraftAsync(Guid authorAccountId, Guid storyId, StoryUpdateRequest request, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Your author account is restricted.", 403);
            }

            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            if (!string.Equals(story.status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("StoryUpdateNotAllowed", "Only draft stories can be edited before submission.", 400);
            }

            var updated = false;

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                story.title = request.Title.Trim();
                updated = true;
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
                    throw new AppException("OutlineRequired", "Story outline must not be empty.", 400);
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
                    throw new AppException("InvalidTag", "One or more tags do not exist.", 400);
                }

                await _storyRepository.ReplaceStoryTagsAsync(story.story_id, tagIds, ct);
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(request.CoverMode))
            {
                var coverMode = request.CoverMode.Trim().ToLowerInvariant();
                if (!string.Equals(coverMode, "upload", StringComparison.OrdinalIgnoreCase))
                {
                    throw new AppException("CoverRegenerationNotAllowed", "AI cover generation is not available while editing an existing draft. Please upload a new image instead.", 400);
                }

                var coverUrl = await ResolveCoverUrlAsync("upload", request.CoverFile, null, ct);
                story.cover_url = coverUrl;
                updated = true;
            }

            if (!updated)
            {
                throw new AppException("NoChanges", "No changes were provided.", 400);
            }

            story.updated_at = TimezoneConverter.VietnamNow;
            await _storyRepository.UpdateStoryAsync(story, ct);

            var refreshed = await _storyRepository.GetStoryForAuthorAsync(story.story_id, author.account_id, ct) ?? story;
            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(refreshed, approvals);
        }

        private async Task<string?> ResolveCoverUrlAsync(string coverMode, IFormFile? coverFile, string? coverPrompt, CancellationToken ct)
        {
            var mode = coverMode.ToLowerInvariant();
            switch (mode)
            {
                case "upload":
                    if (coverFile == null || coverFile.Length == 0)
                    {
                        throw new AppException("CoverRequired", "CoverFile must be provided when CoverMode is 'upload'.", 400);
                    }

                    await using (var stream = coverFile.OpenReadStream())
                    {
                        return await _imageUploader.UploadStoryCoverAsync(stream, coverFile.FileName, ct);
                    }

                case "generate":
                    if (string.IsNullOrWhiteSpace(coverPrompt))
                    {
                        throw new AppException("CoverPromptRequired", "CoverPrompt must be provided when CoverMode is 'generate'.", 400);
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
                    throw new AppException("InvalidCoverMode", "CoverMode must be either 'upload' or 'generate'.", 400);
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
                throw new AppException("InvalidStatus", $"Unsupported status '{status}'. Allowed values are: {string.Join(", ", AuthorListAllowedStatuses)}.", 400);
            }

            return new[] { normalized.ToLowerInvariant() };
        }

        private async Task<author> RequireAuthorAsync(Guid accountId, CancellationToken ct)
        {
            var author = await _storyRepository.GetAuthorAsync(accountId, ct);
            if (author == null)
            {
                throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);
            }
            return author;
        }

        private async Task<content_approve> UpsertStoryApprovalAsync(Guid storyId, string status, decimal aiScore, string? aiNote, CancellationToken ct)
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
                approval.moderator_feedback = null;
                approval.moderator_id = null;
                approval.created_at = timestamp;

                await _storyRepository.SaveChangesAsync(ct);
            }

            return approval;
        }

        private static StoryResponse MapStory(story story, IEnumerable<content_approve> approvals)
        {
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
            var moderatorFeedback = approval?.moderator_id.HasValue == true ? approval.moderator_feedback : null;

            return new StoryResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                Description = story.desc,
                Status = story.status,
                IsPremium = story.is_premium,
                CoverUrl = story.cover_url,
                AiScore = approval?.ai_score,
                AiResult = ResolveAiDecision(approval),
                AiFeedback = approval?.ai_feedback,
                ModeratorStatus = moderatorStatus,
                moderatorFeedback = moderatorFeedback,
                Outline = story.outline,
                LengthPlan = story.length_plan,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                PublishedAt = story.published_at,
                Tags = tags
            };
        }

        private static StoryListItemResponse MapStoryListItem(story story)
        {
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
            var moderatorFeedback = approval?.moderator_id.HasValue == true ? approval.moderator_feedback : null;

            return new StoryListItemResponse
            {
                StoryId = story.story_id,
                Title = story.title,
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
                moderatorFeedback = moderatorFeedback
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

            if (score >= 7m)
            {
                return "approved";
            }

            return "flagged";
        }

        private static string NormalizeLengthPlan(string? plan)
        {
            if (string.IsNullOrWhiteSpace(plan))
            {
                throw new AppException("LengthPlanRequired", "Length plan is required.", 400);
            }

            var normalized = plan.Trim().ToLowerInvariant();
            if (!AllowedLengthPlans.Contains(normalized))
            {
                throw new AppException("InvalidLengthPlan", "Length plan must be novel, short, or super_short.", 400);
            }
            return normalized;
        }
    }
}





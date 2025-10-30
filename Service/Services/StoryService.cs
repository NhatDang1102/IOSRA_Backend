using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Services
{
    public class StoryService : IStoryService
    {
        private readonly IStoryRepository _storyRepository;
        private readonly IImageUploader _imageUploader;
        private readonly IOpenAiImageService _openAiImageService;
        private readonly IOpenAiModerationService _openAiModerationService;

        private static readonly string[] AllowedCoverModes = { "upload", "generate" };

        public StoryService(
            IStoryRepository storyRepository,
            IImageUploader imageUploader,
            IOpenAiImageService openAiImageService,
            IOpenAiModerationService openAiModerationService)
        {
            _storyRepository = storyRepository;
            _imageUploader = imageUploader;
            _openAiImageService = openAiImageService;
            _openAiModerationService = openAiModerationService;
        }

        public async Task<StoryResponse> CreateAsync(ulong authorAccountId, StoryCreateRequest request, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);

            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Your author account is restricted.", 403);
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
            var tags = await _storyRepository.GetTagsByIdsAsync(tagIds, ct);
            if (tags.Count != tagIds.Length)
            {
                throw new AppException("InvalidTag", "One or more tags do not exist.", 400);
            }

            var coverUrl = await ResolveCoverUrlAsync(request, ct);
            var isPremium = author.rank is not null &&
                            !string.Equals(author.rank.rank_name, "Casual", StringComparison.OrdinalIgnoreCase);

            var story = new story
            {
                author_id = author.account_id,
                title = request.Title.Trim(),
                desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
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

        public async Task<StoryResponse> SubmitForReviewAsync(ulong authorAccountId, ulong storyId, StorySubmitRequest request, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            if (author.restricted)
            {
                throw new AppException("AuthorRestricted", "Your author account is restricted.", 403);
            }

            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            var submitNote = string.IsNullOrWhiteSpace(request?.Notes) ? null : request.Notes!.Trim();

            if (story.status == "pending")
            {
                throw new AppException("StoryPending", "Story is already pending moderation.", 400);
            }

            if (story.status == "published")
            {
                throw new AppException("StoryPublished", "Published stories cannot be resubmitted.", 400);
            }

            if (await _storyRepository.AuthorHasPendingStoryAsync(author.account_id, story.story_id, ct))
            {
                throw new AppException("PendingStoryLimit", "You already have another story waiting for moderation.", 400);
            }

            if (await _storyRepository.AuthorHasUncompletedPublishedStoryAsync(author.account_id, ct))
            {
                throw new AppException("PublishedStoryIncomplete", "Please complete your published story before submitting a new one.", 400);
            }

            if (story.status == "rejected")
            {
                var lastRejectedAt = await _storyRepository.GetLastStoryRejectedAtAsync(story.story_id, ct);
                if (lastRejectedAt.HasValue && lastRejectedAt.Value > DateTime.UtcNow.AddDays(-7))
                {
                    throw new AppException("StoryRejectedCooldown", "Please wait 7 days before resubmitting this story.", 400, new
                    {
                        availableAtUtc = lastRejectedAt.Value.AddDays(7)
                    });
                }
            }

            var aiResult = await _openAiModerationService.ModerateStoryAsync(story.title, story.desc, ct);
            var aiScore = (decimal)Math.Round(aiResult.Score, 2, MidpointRounding.AwayFromZero);
            var aiNote = aiResult.Explanation;

            story.updated_at = DateTime.UtcNow;

            if (aiResult.ShouldReject)
            {
                story.status = "rejected";

                await _storyRepository.AddContentApproveAsync(new content_approve
                {
                    approve_type = "story",
                    story_id = story.story_id,
                    source = "ai",
                    status = "rejected",
                    ai_score = aiScore,
                    moderator_note = aiNote
                }, ct);

                throw new AppException("StoryRejectedByAi", "Story was rejected by automated moderation.", 400, new
                {
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

            story.status = "pending";

            await _storyRepository.AddContentApproveAsync(new content_approve
            {
                approve_type = "story",
                story_id = story.story_id,
                source = "ai",
                status = "approved",
                ai_score = aiScore,
                moderator_note = aiNote
            }, ct);

            await _storyRepository.AddContentApproveAsync(new content_approve
            {
                approve_type = "story",
                story_id = story.story_id,
                source = "human",
                status = "pending",
                moderator_note = submitNote
            }, ct);

            var saved = await _storyRepository.GetStoryForAuthorAsync(story.story_id, author.account_id, ct)
                        ?? throw new InvalidOperationException("Failed to load story after submission.");
            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(saved.story_id, ct);

            return MapStory(saved, approvals);
        }

        public async Task<IReadOnlyList<StoryListItemResponse>> ListAsync(ulong authorAccountId, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            var stories = await _storyRepository.GetStoriesByAuthorAsync(author.account_id, ct);

            var responses = new List<StoryListItemResponse>(stories.Count);
            foreach (var story in stories)
            {
                var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
                responses.Add(MapStoryListItem(story, approvals));
            }

            return responses;
        }

        public async Task<StoryResponse> GetAsync(ulong authorAccountId, ulong storyId, CancellationToken ct = default)
        {
            var author = await RequireAuthorAsync(authorAccountId, ct);
            var story = await _storyRepository.GetStoryForAuthorAsync(storyId, author.account_id, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(story, approvals);
        }

        public async Task<StoryResponse> CompleteAsync(ulong authorAccountId, ulong storyId, CancellationToken ct = default)
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

            var earliestCompletion = publishedAt.Value.AddDays(30);
            if (DateTime.UtcNow < earliestCompletion)
            {
                throw new AppException("StoryCompletionCooldown", "Story must be published for at least 30 days before completion.", 400, new
                {
                    availableAtUtc = earliestCompletion
                });
            }

            story.status = "completed";
            story.updated_at = DateTime.UtcNow;

            await _storyRepository.UpdateStoryAsync(story, ct);

            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            return MapStory(story, approvals);
        }

        private async Task<string?> ResolveCoverUrlAsync(StoryCreateRequest request, CancellationToken ct)
        {
            var mode = request.CoverMode.ToLowerInvariant();
            switch (mode)
            {
                case "upload":
                    if (request.CoverFile == null || request.CoverFile.Length == 0)
                    {
                        throw new AppException("CoverRequired", "CoverFile must be provided when CoverMode is 'upload'.", 400);
                    }

                    await using (var stream = request.CoverFile.OpenReadStream())
                    {
                        return await _imageUploader.UploadStoryCoverAsync(stream, request.CoverFile.FileName, ct);
                    }

                case "generate":
                    if (string.IsNullOrWhiteSpace(request.CoverPrompt))
                    {
                        throw new AppException("CoverPromptRequired", "CoverPrompt must be provided when CoverMode is 'generate'.", 400);
                    }

                    var image = await _openAiImageService.GenerateCoverAsync(request.CoverPrompt, ct);
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

        private async Task<author> RequireAuthorAsync(ulong accountId, CancellationToken ct)
        {
            var author = await _storyRepository.GetAuthorAsync(accountId, ct);
            if (author == null)
            {
                throw new AppException("AuthorNotFound", "Author profile is not registered.", 404);
            }
            return author;
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

            var latestAi = approvals.FirstOrDefault(a => a.source == "ai");
            var latestHuman = approvals.FirstOrDefault(a => a.source == "human");

            return new StoryResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                Description = story.desc,
                Status = story.status,
                IsPremium = story.is_premium,
                CoverUrl = story.cover_url,
                AiScore = latestAi?.ai_score,
                AiResult = latestAi?.status,
                ModeratorStatus = latestHuman?.status,
                ModeratorNote = latestHuman?.moderator_note,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                PublishedAt = story.published_at,
                Tags = tags
            };
        }

        private static StoryListItemResponse MapStoryListItem(story story, IEnumerable<content_approve> approvals)
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

            return new StoryListItemResponse
            {
                StoryId = story.story_id,
                Title = story.title,
                Status = story.status,
                IsPremium = story.is_premium,
                CoverUrl = story.cover_url,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                PublishedAt = story.published_at,
                Tags = tags
            };
        }
    }
}



using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;
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
    public class StoryModerationService : IStoryModerationService
    {
        private readonly IStoryRepository _storyRepository;
        private readonly IMailSender _mailSender;

        public StoryModerationService(IStoryRepository storyRepository, IMailSender mailSender)
        {
            _storyRepository = storyRepository;
            _mailSender = mailSender;
        }

        public async Task<IReadOnlyList<StoryModerationQueueItem>> ListPendingAsync(CancellationToken ct = default)
        {
            var stories = await _storyRepository.GetStoriesPendingHumanReviewAsync(ct);
            var response = new List<StoryModerationQueueItem>(stories.Count);

            foreach (var story in stories)
            {
                var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
                var aiReview = approvals.FirstOrDefault(a => a.source == "ai");
                var pending = approvals.FirstOrDefault(a => a.source == "human" && a.status == "pending");

                var tags = story.story_tags?
                    .Where(st => st.tag != null)
                    .Select(st => new StoryTagResponse
                    {
                        TagId = st.tag_id,
                        TagName = st.tag.tag_name
                    })
                    .OrderBy(t => t.TagName, StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<StoryTagResponse>();

                response.Add(new StoryModerationQueueItem
                {
                    StoryId = story.story_id,
                    Title = story.title,
                    Description = story.desc,
                    AuthorId = story.author_id,
                    AuthorUsername = story.author.account.username,
                    CoverUrl = story.cover_url,
                    AiScore = aiReview?.ai_score,
                    AiResult = aiReview?.status,
                    SubmittedAt = pending?.created_at ?? story.updated_at,
                    PendingNote = pending?.moderator_note,
                    Tags = tags
                });
            }

            return response;
        }

        public async Task ModerateAsync(ulong moderatorAccountId, ulong storyId, StoryModerationDecisionRequest request, CancellationToken ct = default)
        {
            var story = await _storyRepository.GetStoryWithDetailsAsync(storyId, ct)
                        ?? throw new AppException("StoryNotFound", "Story was not found.", 404);

            if (story.status != "pending")
            {
                throw new AppException("StoryNotPending", "Story is not awaiting moderation.", 400);
            }

            var approvals = await _storyRepository.GetContentApprovalsForStoryAsync(story.story_id, ct);
            var pending = approvals.FirstOrDefault(a => a.source == "human" && a.status == "pending");
            if (pending == null)
            {
                throw new AppException("PendingEntryMissing", "No pending moderation entry was found for this story.", 400);
            }

            pending.status = request.Approve ? "approved" : "rejected";
            pending.moderator_id = moderatorAccountId;
            pending.moderator_note = string.IsNullOrWhiteSpace(request.ModeratorNote) ? null : request.ModeratorNote.Trim();

            var authorRank = story.author.rank?.rank_name;
            story.is_premium = !string.IsNullOrWhiteSpace(authorRank) && !string.Equals(authorRank, "Casual", StringComparison.OrdinalIgnoreCase);
            if (request.Approve)
            {
                story.status = "published";
                story.published_at ??= DateTime.UtcNow;
            }
            else
            {
                story.status = "rejected";
            }
            story.updated_at = DateTime.UtcNow;

            await _storyRepository.SaveChangesAsync(ct);

            var authorEmail = story.author.account.email;
            if (request.Approve)
            {
                await _mailSender.SendStoryApprovedEmailAsync(authorEmail, story.title);
            }
            else
            {
                await _mailSender.SendStoryRejectedEmailAsync(authorEmail, story.title, pending.moderator_note);
            }
        }
    }
}


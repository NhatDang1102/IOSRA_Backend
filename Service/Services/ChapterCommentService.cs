using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Contract.DTOs.Respond.Common;
using Repository.Entities;
using Repository.Interfaces;
using Repository.DataModels;
using Repository.Utils;
using Service.Constants;
using Service.Exceptions;
using Service.Interfaces;
using Service.Helpers;

namespace Service.Services
{
    public class ChapterCommentService : IChapterCommentService
    {
        private const int MaxPageSize = 100;
        private static readonly string[] PublicStoryStatuses = { "published", "completed" };
        private static readonly string[] ModerationStatuses = { "visible", "hidden", "removed" };

        private readonly IChapterCommentRepository _commentRepository;
        private readonly IStoryCatalogRepository _storyCatalogRepository;
        private readonly IProfileRepository _profileRepository;
        private readonly INotificationService _notificationService;

        public ChapterCommentService(
            IChapterCommentRepository commentRepository,
            IStoryCatalogRepository storyCatalogRepository,
            IProfileRepository profileRepository,
            INotificationService notificationService)
        {
            _commentRepository = commentRepository;
            _storyCatalogRepository = storyCatalogRepository;
            _profileRepository = profileRepository;
            _notificationService = notificationService;
        }

        public async Task<PagedResult<ChapterCommentResponse>> GetByChapterAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default, Guid? viewerAccountId = null)
        {
            var chapter = await RequirePublishedChapterAsync(chapterId, ct);
            var normalizedPage = NormalizePage(page);
            var normalizedSize = NormalizePageSize(pageSize);
            var (items, total) = await _commentRepository.GetByChapterAsync(chapter.chapter_id, normalizedPage, normalizedSize, ct);
            var aggregates = await LoadAggregatesAsync(items, viewerAccountId, ct);

            return new PagedResult<ChapterCommentResponse>
            {
                Items = items.Select(c => MapPublicComment(c, TryGetAggregate(aggregates, c.comment_id))).ToArray(),
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        public async Task<StoryCommentFeedResponse> GetByStoryAsync(Guid storyId, Guid? chapterId, int page, int pageSize, CancellationToken ct = default, Guid? viewerAccountId = null)
        {
            var story = await RequirePublishedStoryAsync(storyId, ct);
            Guid? chapterFilterId = null;
            if (chapterId.HasValue && chapterId.Value != Guid.Empty)
            {
                var chapter = await _commentRepository.GetChapterWithStoryAsync(chapterId.Value, ct)
                              ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);
                if (chapter.story_id != story.story_id)
                {
                    throw new AppException("ChapterNotInStory", "Chapter does not belong to the requested story.", 400);
                }
                chapterFilterId = chapter.chapter_id;
            }

            var normalizedPage = NormalizePage(page);
            var normalizedSize = NormalizePageSize(pageSize);
            var (items, total) = await _commentRepository.GetByStoryAsync(story.story_id, chapterFilterId, normalizedPage, normalizedSize, ct);
            var aggregates = await LoadAggregatesAsync(items, viewerAccountId, ct);

            return new StoryCommentFeedResponse
            {
                StoryId = story.story_id,
                ChapterFilterId = chapterFilterId,
                Comments = new PagedResult<ChapterCommentResponse>
                {
                    Items = items.Select(c => MapPublicComment(c, TryGetAggregate(aggregates, c.comment_id))).ToArray(),
                    Total = total,
                    Page = normalizedPage,
                    PageSize = normalizedSize
                }
            };
        }

        public async Task<PagedResult<ChapterCommentModerationResponse>> GetForModerationAsync(string? status, Guid? storyId, Guid? chapterId, Guid? readerId, int page, int pageSize, CancellationToken ct = default)
        {
            var normalizedPage = NormalizePage(page);
            var normalizedSize = NormalizePageSize(pageSize);
            var normalizedStatus = NormalizeModerationStatus(status);
            var normalizedStoryId = NormalizeFilterGuid(storyId);
            var normalizedChapterId = NormalizeFilterGuid(chapterId);
            var normalizedReaderId = NormalizeFilterGuid(readerId);

            var (items, total) = await _commentRepository.GetForModerationAsync(normalizedStatus, normalizedStoryId, normalizedChapterId, normalizedReaderId, normalizedPage, normalizedSize, ct);

            return new PagedResult<ChapterCommentModerationResponse>
            {
                Items = items.Select(MapModerationComment).ToArray(),
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        public async Task<ChapterCommentResponse> CreateAsync(Guid readerAccountId, Guid chapterId, ChapterCommentCreateRequest request, CancellationToken ct = default)
        {
            var reader = await _profileRepository.GetReaderByIdAsync(readerAccountId, ct)
                         ?? throw new AppException("ReaderProfileMissing", "Reader profile is not registered.", 404);
            var chapter = await RequirePublishedChapterAsync(chapterId, ct);

            await AccountRestrictionHelper.EnsureCanPublishAsync(reader.account, _profileRepository, ct);

            var content = (request.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AppException("InvalidCommentContent", "Comment content must not be empty.", 400);
            }

            var now = TimezoneConverter.VietnamNow;
            var comment = new chapter_comment
            {
                comment_id = Guid.NewGuid(),
                reader_id = reader.account_id,
                story_id = chapter.story_id,
                chapter_id = chapter.chapter_id,
                content = content,
                status = "visible",
                is_locked = false,
                created_at = now,
                updated_at = now
            };

            await _commentRepository.AddAsync(comment, ct);
            var saved = await _commentRepository.GetAsync(comment.chapter_id, comment.comment_id, ct)
                        ?? throw new InvalidOperationException("Failed to load comment after creation.");

            await NotifyAuthorCommentAsync(chapter, reader, saved, ct);
            return MapPublicComment(saved);
        }

        public async Task<ChapterCommentResponse> UpdateAsync(Guid readerAccountId, Guid chapterId, Guid commentId, ChapterCommentUpdateRequest request, CancellationToken ct = default)
        {
            var comment = await _commentRepository.GetForOwnerAsync(chapterId, commentId, readerAccountId, ct)
                          ?? throw new AppException("CommentNotFound", "Comment was not found.", 404);

            if (!string.Equals(comment.status, "visible", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("CommentNotEditable", "Comment cannot be edited in its current state.", 400);
            }

            if (comment.is_locked)
            {
                throw new AppException("CommentLocked", "Comment has been locked by a moderator.", 403);
            }

            var content = (request.Content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AppException("InvalidCommentContent", "Comment content must not be empty.", 400);
            }

            comment.content = content;
            comment.updated_at = TimezoneConverter.VietnamNow;

            await _commentRepository.UpdateAsync(comment, ct);
            var refreshed = await _commentRepository.GetAsync(comment.chapter_id, comment.comment_id, ct)
                             ?? throw new InvalidOperationException("Failed to load comment after update.");
            return MapPublicComment(refreshed);
        }

        public async Task DeleteAsync(Guid readerAccountId, Guid chapterId, Guid commentId, CancellationToken ct = default)
        {
            var comment = await _commentRepository.GetForOwnerAsync(chapterId, commentId, readerAccountId, ct)
                          ?? throw new AppException("CommentNotFound", "Comment was not found.", 404);

            if (comment.is_locked)
            {
                throw new AppException("CommentLocked", "Comment has been locked by a moderator.", 403);
            }

            comment.status = "removed";
            comment.updated_at = TimezoneConverter.VietnamNow;
            await _commentRepository.UpdateAsync(comment, ct);
        }

        public async Task<ChapterCommentResponse> ReactAsync(Guid readerAccountId, Guid chapterId, Guid commentId, ChapterCommentReactRequest request, CancellationToken ct = default)
        {
            var reader = await _profileRepository.GetReaderByIdAsync(readerAccountId, ct)
                         ?? throw new AppException("ReaderProfileMissing", "Reader profile is not registered.", 404);
            var normalizedReaction = NormalizeReactionType(request.ReactionType);
            var comment = await RequireVisibleCommentAsync(chapterId, commentId, ensureUnlocked: true, ct);
            var existingReaction = await _commentRepository.GetReactionAsync(comment.comment_id, reader.account_id, ct);
            var now = TimezoneConverter.VietnamNow;

            if (existingReaction == null)
            {
                var reaction = new chapter_comment_reaction
                {
                    reaction_id = Guid.NewGuid(),
                    comment_id = comment.comment_id,
                    reader_id = reader.account_id,
                    reaction_type = normalizedReaction,
                    created_at = now,
                    updated_at = now
                };
                await _commentRepository.AddReactionAsync(reaction, ct);
            }
            else if (!string.Equals(existingReaction.reaction_type, normalizedReaction, StringComparison.OrdinalIgnoreCase))
            {
                existingReaction.reaction_type = normalizedReaction;
                existingReaction.updated_at = now;
                await _commentRepository.UpdateReactionAsync(existingReaction, ct);
            }

            var aggregate = await LoadAggregateAsync(comment.comment_id, reader.account_id, ct);
            var refreshed = await _commentRepository.GetAsync(comment.chapter_id, comment.comment_id, ct)
                             ?? throw new InvalidOperationException("Failed to load comment after reacting.");
            return MapPublicComment(refreshed, aggregate);
        }

        public async Task<ChapterCommentResponse> RemoveReactionAsync(Guid readerAccountId, Guid chapterId, Guid commentId, CancellationToken ct = default)
        {
            var comment = await RequireVisibleCommentAsync(chapterId, commentId, ensureUnlocked: true, ct);
            var existingReaction = await _commentRepository.GetReactionAsync(comment.comment_id, readerAccountId, ct);
            if (existingReaction != null)
            {
                await _commentRepository.RemoveReactionAsync(existingReaction, ct);
            }

            var aggregate = await LoadAggregateAsync(comment.comment_id, readerAccountId, ct);
            var refreshed = await _commentRepository.GetAsync(comment.chapter_id, comment.comment_id, ct)
                             ?? throw new InvalidOperationException("Failed to load comment after removing reaction.");
            return MapPublicComment(refreshed, aggregate);
        }

        public async Task<PagedResult<ChapterCommentReactionUserResponse>> GetReactionsAsync(Guid chapterId, Guid commentId, string reactionType, int page, int pageSize, CancellationToken ct = default)
        {
            var normalizedReaction = NormalizeReactionType(reactionType);
            var comment = await RequireVisibleCommentAsync(chapterId, commentId, ensureUnlocked: false, ct);
            var normalizedPage = NormalizePage(page);
            var normalizedSize = NormalizePageSize(pageSize);
            var (items, total) = await _commentRepository.GetReactionsAsync(comment.comment_id, normalizedReaction, normalizedPage, normalizedSize, ct);

            return new PagedResult<ChapterCommentReactionUserResponse>
            {
                Items = items.Select(MapReactionUser).ToArray(),
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        private async Task<story> RequirePublishedStoryAsync(Guid storyId, CancellationToken ct)
        {
            var story = await _storyCatalogRepository.GetPublishedStoryByIdAsync(storyId, ct);
            if (story == null)
            {
                throw new AppException("StoryNotFound", "Story was not found or unavailable.", 404);
            }
            return story;
        }

        private async Task<chapter> RequirePublishedChapterAsync(Guid chapterId, CancellationToken ct)
        {
            var chapter = await _commentRepository.GetChapterWithStoryAsync(chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            EnsureChapterIsPublic(chapter);
            return chapter;
        }

        private async Task NotifyAuthorCommentAsync(chapter chapter, reader commenter, chapter_comment comment, CancellationToken ct)
        {
            var story = chapter.story;
            var author = story?.author;
            var authorAccount = author?.account;
            if (story == null || authorAccount == null)
            {
                return;
            }

            if (authorAccount.account_id == commenter.account_id)
            {
                return;
            }

            var commenterName = commenter.account.username;
            var chapterNo = (int)chapter.chapter_no;
            var title = $"{commenterName} vừa bình luận trên truyện của bạn";
            var message = $"{commenterName}: \"{Truncate(comment.content, 80)}\" (Chương {chapterNo} - \"{chapter.title}\").";

            await _notificationService.CreateAsync(new NotificationCreateModel(
                authorAccount.account_id,
                NotificationTypes.ChapterComment,
                title,
                message,
                new
                {
                    storyId = story.story_id,
                    chapterId = chapter.chapter_id,
                    commentId = comment.comment_id
                }), ct);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static ChapterCommentResponse MapPublicComment(chapter_comment comment, ChapterCommentReactionAggregate? reactionStats = null)
        {
            var readerAccount = comment.reader?.account
                               ?? throw new InvalidOperationException("Comment reader account navigation not loaded.");
            var chapter = comment.chapter ?? throw new InvalidOperationException("Comment chapter navigation not loaded.");

            return new ChapterCommentResponse
            {
                CommentId = comment.comment_id,
                StoryId = comment.story_id,
                ChapterId = comment.chapter_id,
                ChapterNo = (int)chapter.chapter_no,
                ChapterTitle = chapter.title,
                ReaderId = comment.reader_id,
                Username = readerAccount.username,
                AvatarUrl = readerAccount.avatar_url,
                Content = comment.content,
                IsLocked = comment.is_locked,
                CreatedAt = comment.created_at,
                UpdatedAt = comment.updated_at,
                LikeCount = reactionStats?.LikeCount ?? 0,
                DislikeCount = reactionStats?.DislikeCount ?? 0,
                ViewerReaction = reactionStats?.ViewerReaction
            };
        }

        private static ChapterCommentModerationResponse MapModerationComment(chapter_comment comment)
        {
            var readerAccount = comment.reader?.account
                               ?? throw new InvalidOperationException("Comment reader account navigation not loaded.");
            var chapter = comment.chapter ?? throw new InvalidOperationException("Comment chapter navigation not loaded.");
            var story = chapter.story ?? throw new InvalidOperationException("Comment story navigation not loaded.");

            return new ChapterCommentModerationResponse
            {
                CommentId = comment.comment_id,
                StoryId = comment.story_id,
                StoryTitle = story.title,
                ChapterId = comment.chapter_id,
                ChapterNo = (int)chapter.chapter_no,
                ChapterTitle = chapter.title,
                ReaderId = comment.reader_id,
                Username = readerAccount.username,
                AvatarUrl = readerAccount.avatar_url,
                Content = comment.content,
                Status = comment.status,
                IsLocked = comment.is_locked,
                CreatedAt = comment.created_at,
                UpdatedAt = comment.updated_at
            };
        }

        private async Task<chapter_comment> RequireVisibleCommentAsync(Guid chapterId, Guid commentId, bool ensureUnlocked, CancellationToken ct)
        {
            var comment = await _commentRepository.GetAsync(chapterId, commentId, ct)
                          ?? throw new AppException("CommentNotFound", "Comment was not found.", 404);

            var chapter = comment.chapter ?? throw new InvalidOperationException("Comment chapter navigation not loaded.");
            EnsureChapterIsPublic(chapter);

            if (!string.Equals(comment.status, "visible", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("CommentUnavailable", "Comment is not available in its current state.", 400);
            }

            if (ensureUnlocked && comment.is_locked)
            {
                throw new AppException("CommentLocked", "Comment has been locked by a moderator.", 403);
            }

            return comment;
        }

        private async Task<Dictionary<Guid, ChapterCommentReactionAggregate>> LoadAggregatesAsync(IEnumerable<chapter_comment> comments, Guid? viewerAccountId, CancellationToken ct)
        {
            var ids = comments?.Select(c => c.comment_id).Distinct().ToArray() ?? Array.Empty<Guid>();
            if (ids.Length == 0)
            {
                return new Dictionary<Guid, ChapterCommentReactionAggregate>();
            }

            return await _commentRepository.GetReactionAggregatesAsync(ids, viewerAccountId, ct);
        }

        private async Task<ChapterCommentReactionAggregate?> LoadAggregateAsync(Guid commentId, Guid? viewerAccountId, CancellationToken ct)
        {
            var map = await _commentRepository.GetReactionAggregatesAsync(new[] { commentId }, viewerAccountId, ct);
            return TryGetAggregate(map, commentId);
        }

        private static ChapterCommentReactionAggregate? TryGetAggregate(Dictionary<Guid, ChapterCommentReactionAggregate>? aggregates, Guid commentId)
        {
            if (aggregates == null)
            {
                return null;
            }

            return aggregates.TryGetValue(commentId, out var aggregate) ? aggregate : null;
        }

        private static string NormalizeReactionType(string? reactionType)
        {
            if (string.IsNullOrWhiteSpace(reactionType))
            {
                throw new AppException("InvalidReactionType", "Reaction type is required.", 400);
            }

            var normalized = reactionType.Trim().ToLowerInvariant();
            if (!ChapterCommentReactionTypes.Allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidReactionType", $"Unsupported reaction type '{reactionType}'. Allowed values: {string.Join(", ", ChapterCommentReactionTypes.Allowed)}.", 400);
            }

            return normalized;
        }

        private static ChapterCommentReactionUserResponse MapReactionUser(chapter_comment_reaction reaction)
        {
            var readerAccount = reaction.reader?.account
                               ?? throw new InvalidOperationException("Reaction reader account navigation not loaded.");

            return new ChapterCommentReactionUserResponse
            {
                ReaderId = reaction.reader_id,
                Username = readerAccount.username,
                AvatarUrl = readerAccount.avatar_url,
                ReactionType = reaction.reaction_type,
                CreatedAt = reaction.created_at
            };
        }

        private static void EnsureChapterIsPublic(chapter chapter)
        {
            if (!string.Equals(chapter.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotPublished", "Comments are only allowed on published chapters.", 400);
            }

            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation not loaded.");
            if (!PublicStoryStatuses.Contains(story.status, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("StoryNotPublished", "Comments are only allowed when the story is published.", 400);
            }
        }

        private static int NormalizePage(int page) => page <= 0 ? 1 : page;
        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0) return 20;
            return pageSize > MaxPageSize ? MaxPageSize : pageSize;
        }

        private static string? NormalizeModerationStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var normalized = status.Trim().ToLowerInvariant();
            if (!ModerationStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("InvalidCommentStatus", $"Unsupported status '{status}'. Allowed values: {string.Join(", ", ModerationStatuses)}.", 400);
            }

            return normalized;
        }

        private static Guid? NormalizeFilterGuid(Guid? value)
        {
            if (!value.HasValue || value == Guid.Empty)
            {
                return null;
            }
            return value;
        }
    }
}

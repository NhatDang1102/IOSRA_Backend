using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
using Contract.DTOs.Respond.Common;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;
using Service.Interfaces;

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

        public ChapterCommentService(
            IChapterCommentRepository commentRepository,
            IStoryCatalogRepository storyCatalogRepository,
            IProfileRepository profileRepository)
        {
            _commentRepository = commentRepository;
            _storyCatalogRepository = storyCatalogRepository;
            _profileRepository = profileRepository;
        }

        public async Task<PagedResult<ChapterCommentResponse>> GetByChapterAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default)
        {
            var chapter = await RequirePublishedChapterAsync(chapterId, ct);
            var normalizedPage = NormalizePage(page);
            var normalizedSize = NormalizePageSize(pageSize);
            var (items, total) = await _commentRepository.GetByChapterAsync(chapter.chapter_id, normalizedPage, normalizedSize, ct);

            return new PagedResult<ChapterCommentResponse>
            {
                Items = items.Select(MapPublicComment).ToArray(),
                Total = total,
                Page = normalizedPage,
                PageSize = normalizedSize
            };
        }

        public async Task<StoryCommentFeedResponse> GetByStoryAsync(Guid storyId, Guid? chapterId, int page, int pageSize, CancellationToken ct = default)
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

            return new StoryCommentFeedResponse
            {
                StoryId = story.story_id,
                ChapterFilterId = chapterFilterId,
                Comments = new PagedResult<ChapterCommentResponse>
                {
                    Items = items.Select(MapPublicComment).ToArray(),
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

            if (!string.Equals(chapter.status, "published", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotPublished", "Comments are only allowed on published chapters.", 400);
            }

            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation not loaded.");
            if (!PublicStoryStatuses.Contains(story.status, StringComparer.OrdinalIgnoreCase))
            {
                throw new AppException("StoryNotPublished", "Comments are only allowed when the story is published.", 400);
            }

            return chapter;
        }

        private static ChapterCommentResponse MapPublicComment(chapter_comment comment)
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
                UpdatedAt = comment.updated_at
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

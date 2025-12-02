using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repository.Base;
using Repository.DBContext;
using Repository.Entities;
using Repository.Interfaces;
using Repository.DataModels;

namespace Repository.Repositories
{
    public class ChapterCommentRepository : BaseRepository, IChapterCommentRepository
    {
        private const string ReactionLike = "like";
        private const string ReactionDislike = "dislike";
        private static readonly string[] VisibleStatuses = { "visible" };
        private static readonly string[] AllowedStatuses = { "visible", "hidden", "removed" };

        public ChapterCommentRepository(AppDbContext db) : base(db)
        {
        }

        public Task<chapter?> GetChapterWithStoryAsync(Guid chapterId, CancellationToken ct = default)
            => _db.chapter
                  .Include(c => c.story).ThenInclude(s => s.author).ThenInclude(a => a.account)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId, ct);

        public async Task<(List<chapter_comment> Items, int Total)> GetByChapterAsync(Guid chapterId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.chapter_comments
                .AsNoTracking()
                .Include(c => c.reader).ThenInclude(r => r.account)
                .Include(c => c.chapter)
                .Include(c => c.replies.Where(r => VisibleStatuses.Contains(r.status)))
                    .ThenInclude(r => r.reader).ThenInclude(a => a.account)
                .Include(c => c.replies.Where(r => VisibleStatuses.Contains(r.status)))
                    .ThenInclude(r => r.chapter).ThenInclude(ch => ch.story)
                .Where(c => c.chapter_id == chapterId
                            && c.parent_comment_id == null
                            && VisibleStatuses.Contains(c.status));

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(c => c.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<(List<chapter_comment> Items, int Total)> GetByStoryAsync(Guid storyId, Guid? chapterId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.chapter_comments
                .AsNoTracking()
                .Include(c => c.reader).ThenInclude(r => r.account)
                .Include(c => c.chapter)
                .Include(c => c.replies.Where(r => VisibleStatuses.Contains(r.status)))
                    .ThenInclude(r => r.reader).ThenInclude(a => a.account)
                .Include(c => c.replies.Where(r => VisibleStatuses.Contains(r.status)))
                    .ThenInclude(r => r.chapter).ThenInclude(ch => ch.story)
                .Where(c => c.story_id == storyId
                            && c.parent_comment_id == null
                            && VisibleStatuses.Contains(c.status));

            if (chapterId.HasValue && chapterId.Value != Guid.Empty)
            {
                query = query.Where(c => c.chapter_id == chapterId.Value);
            }

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(c => c.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<(List<chapter_comment> Items, int Total)> GetForModerationAsync(string? status, Guid? storyId, Guid? chapterId, Guid? readerId, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.chapter_comments
                .AsNoTracking()
                .Include(c => c.reader).ThenInclude(r => r.account)
                .Include(c => c.chapter).ThenInclude(ch => ch.story)
                .Include(c => c.parent_comment)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalized = status.Trim().ToLowerInvariant();
                if (AllowedStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.status == normalized);
                }
            }

            if (storyId.HasValue && storyId.Value != Guid.Empty)
            {
                query = query.Where(c => c.story_id == storyId.Value);
            }

            if (chapterId.HasValue && chapterId.Value != Guid.Empty)
            {
                query = query.Where(c => c.chapter_id == chapterId.Value);
            }

            if (readerId.HasValue && readerId.Value != Guid.Empty)
            {
                query = query.Where(c => c.reader_id == readerId.Value);
            }

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(c => c.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public Task<chapter_comment?> GetAsync(Guid chapterId, Guid commentId, CancellationToken ct = default)
            => _db.chapter_comments
                  .Include(c => c.reader).ThenInclude(r => r.account)
                  .Include(c => c.chapter).ThenInclude(ch => ch.story)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.comment_id == commentId, ct);

        public Task<chapter_comment?> GetForOwnerAsync(Guid chapterId, Guid commentId, Guid readerId, CancellationToken ct = default)
            => _db.chapter_comments
                  .Include(c => c.reader).ThenInclude(r => r.account)
                  .Include(c => c.chapter).ThenInclude(ch => ch.story)
                  .FirstOrDefaultAsync(c => c.chapter_id == chapterId && c.comment_id == commentId && c.reader_id == readerId, ct);

        public async Task AddAsync(chapter_comment comment, CancellationToken ct = default)
        {
            EnsureId(comment, nameof(chapter_comment.comment_id));
            _db.chapter_comments.Add(comment);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(chapter_comment comment, CancellationToken ct = default)
        {
            _db.chapter_comments.Update(comment);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<Dictionary<Guid, ChapterCommentReactionAggregate>> GetReactionAggregatesAsync(Guid[] commentIds, Guid? viewerAccountId, CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, ChapterCommentReactionAggregate>();
            if (commentIds == null || commentIds.Length == 0)
            {
                return result;
            }

            var distinctIds = commentIds.Distinct().ToArray();
            foreach (var id in distinctIds)
            {
                result[id] = new ChapterCommentReactionAggregate { CommentId = id, LikeCount = 0, DislikeCount = 0 };
            }

            var aggregated = await _db.chapter_comment_reactions
                .AsNoTracking()
                .Where(r => distinctIds.Contains(r.comment_id))
                .GroupBy(r => new { r.comment_id, r.reaction_type })
                .Select(g => new { g.Key.comment_id, g.Key.reaction_type, Count = g.Count() })
                .ToListAsync(ct);

            foreach (var entry in aggregated)
            {
                if (!result.TryGetValue(entry.comment_id, out var aggregate))
                {
                    continue;
                }

                if (string.Equals(entry.reaction_type, ReactionLike, StringComparison.OrdinalIgnoreCase))
                {
                    aggregate.LikeCount = entry.Count;
                }
                else if (string.Equals(entry.reaction_type, ReactionDislike, StringComparison.OrdinalIgnoreCase))
                {
                    aggregate.DislikeCount = entry.Count;
                }
            }

            if (viewerAccountId.HasValue)
            {
                var viewerReactions = await _db.chapter_comment_reactions
                    .AsNoTracking()
                    .Where(r => r.reader_id == viewerAccountId.Value && distinctIds.Contains(r.comment_id))
                    .Select(r => new { r.comment_id, r.reaction_type })
                    .ToListAsync(ct);

                foreach (var entry in viewerReactions)
                {
                    if (result.TryGetValue(entry.comment_id, out var aggregate))
                    {
                        aggregate.ViewerReaction = entry.reaction_type;
                    }
                }
            }

            return result;
        }

        public Task<chapter_comment_reaction?> GetReactionAsync(Guid commentId, Guid readerId, CancellationToken ct = default)
            => _db.chapter_comment_reactions
                .FirstOrDefaultAsync(r => r.comment_id == commentId && r.reader_id == readerId, ct);

        public async Task AddReactionAsync(chapter_comment_reaction reaction, CancellationToken ct = default)
        {
            EnsureId(reaction, nameof(chapter_comment_reaction.reaction_id));
            _db.chapter_comment_reactions.Add(reaction);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdateReactionAsync(chapter_comment_reaction reaction, CancellationToken ct = default)
        {
            _db.chapter_comment_reactions.Update(reaction);
            await _db.SaveChangesAsync(ct);
        }

        public async Task RemoveReactionAsync(chapter_comment_reaction reaction, CancellationToken ct = default)
        {
            _db.chapter_comment_reactions.Remove(reaction);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<(List<chapter_comment_reaction> Items, int Total)> GetReactionsAsync(Guid commentId, string reactionType, int page, int pageSize, CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _db.chapter_comment_reactions
                .AsNoTracking()
                .Include(r => r.reader).ThenInclude(rd => rd.account)
                .Where(r => r.comment_id == commentId);

            if (!string.IsNullOrWhiteSpace(reactionType))
            {
                query = query.Where(r => r.reaction_type == reactionType);
            }

            var total = await query.CountAsync(ct);
            var skip = (page - 1) * pageSize;
            var items = await query
                .OrderByDescending(r => r.created_at)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

    }
}

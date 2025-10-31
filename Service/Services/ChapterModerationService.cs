using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Respond.Chapter;
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
    public class ChapterModerationService : IChapterModerationService
    {
        private readonly IChapterRepository _chapterRepository;
        private readonly IMailSender _mailSender;

        public ChapterModerationService(IChapterRepository chapterRepository, IMailSender mailSender)
        {
            _chapterRepository = chapterRepository;
            _mailSender = mailSender;
        }

        public async Task<IReadOnlyList<ChapterModerationQueueItem>> ListPendingAsync(CancellationToken ct = default)
        {
            var chapters = await _chapterRepository.GetPendingForModerationAsync(ct);
            return chapters.Select(MapQueueItem).ToArray();
        }

        public async Task ModerateAsync(ulong moderatorAccountId, ulong chapterId, ChapterModerationDecisionRequest request, CancellationToken ct = default)
        {
            var chapter = await _chapterRepository.GetByIdAsync(chapterId, ct)
                          ?? throw new AppException("ChapterNotFound", "Chapter was not found.", 404);

            if (!string.Equals(chapter.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ChapterNotPending", "Chapter is not awaiting moderation.", 400);
            }

            var approvals = await _chapterRepository.GetContentApprovalsForChapterAsync(chapter.chapter_id, ct);
            var pending = approvals.FirstOrDefault(a => a.source == "human" && a.status == "pending")
                          ?? throw new AppException("PendingEntryMissing", "No pending moderation entry was found for this chapter.", 400);

            pending.status = request.Approve ? "approved" : "rejected";
            pending.moderator_id = moderatorAccountId;
            pending.moderator_note = string.IsNullOrWhiteSpace(request.ModeratorNote) ? pending.moderator_note : request.ModeratorNote!.Trim();

            if (request.Approve)
            {
                chapter.status = "published";
                chapter.published_at ??= DateTime.UtcNow;
            }
            else
            {
                chapter.status = "rejected";
                chapter.ai_feedback = string.IsNullOrWhiteSpace(request.ModeratorNote) ? chapter.ai_feedback : request.ModeratorNote!.Trim();
            }

            chapter.updated_at = DateTime.UtcNow;

            await _chapterRepository.UpdateAsync(chapter, ct);

            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            var author = story.author ?? throw new InvalidOperationException("Story author navigation was not loaded.");
            var authorAccount = author.account ?? throw new InvalidOperationException("Author account navigation was not loaded.");

            if (request.Approve)
            {
                await _mailSender.SendChapterApprovedEmailAsync(authorAccount.email, story.title, chapter.title);
            }
            else
            {
                await _mailSender.SendChapterRejectedEmailAsync(authorAccount.email, story.title, chapter.title, request.ModeratorNote);
            }
        }

        private static ChapterModerationQueueItem MapQueueItem(chapter chapter)
        {
            var story = chapter.story ?? throw new InvalidOperationException("Chapter story navigation was not loaded.");
            var author = story.author ?? throw new InvalidOperationException("Story author navigation was not loaded.");
            var account = author.account ?? throw new InvalidOperationException("Author account navigation was not loaded.");
            var language = chapter.language ?? throw new InvalidOperationException("Chapter language navigation was not loaded.");

            return new ChapterModerationQueueItem
            {
                ChapterId = chapter.chapter_id,
                StoryId = chapter.story_id,
                StoryTitle = story.title,
                ChapterTitle = chapter.title,
                AuthorId = author.account_id,
                AuthorUsername = account.username,
                AuthorEmail = account.email,
                ChapterNo = (int)chapter.chapter_no,
                WordCount = chapter.word_count,
                LanguageCode = language.lang_code,
                LanguageName = language.lang_name,
                PriceDias = (int)chapter.dias_price,
                AiScore = chapter.ai_score,
                AiFeedback = chapter.ai_feedback,
                Status = chapter.status,
                SubmittedAt = chapter.submitted_at ?? chapter.updated_at,
                CreatedAt = chapter.created_at
            };
        }
    }
}

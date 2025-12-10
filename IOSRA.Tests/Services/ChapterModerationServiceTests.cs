using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Request.OperationMod;   // ChapterModerationDecisionRequest
using Contract.DTOs.Response.Notification;   // NotificationResponse
using Contract.DTOs.Response.OperationMod;   // ChapterModerationQueueItem
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Service.Models;  // NotificationCreateModel (nếu model nằm namespace khác thì chỉnh lại)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class ChapterModerationServiceTests
    {
        private readonly Mock<IChapterModerationRepository> _repo;
        private readonly Mock<IMailSender> _mail;
        private readonly Mock<INotificationService> _noti;
        private readonly Mock<IFollowerNotificationService> _followers;
        private readonly Mock<IContentModRepository> _contentModRepo;
        private readonly ChapterModerationService _svc;

        public ChapterModerationServiceTests()
        {
            _repo = new Mock<IChapterModerationRepository>(MockBehavior.Strict);
            _mail = new Mock<IMailSender>(MockBehavior.Strict);
            _noti = new Mock<INotificationService>(MockBehavior.Strict);
            _followers = new Mock<IFollowerNotificationService>(MockBehavior.Strict);
            _contentModRepo = new Mock<IContentModRepository>(MockBehavior.Strict);

            _svc = new ChapterModerationService(
                _repo.Object,
                _mail.Object,
                _noti.Object,
                _followers.Object,
                _contentModRepo.Object
            );
        }

        #region Helpers

        private static account MakeAccount(Guid id, string username = "author01", string email = "author@test.com")
            => new()
            {
                account_id = id,
                username = username,
                email = email,
                avatar_url = "a.png",
                status = "unbanned",
                strike = 0
            };

        private static author MakeAuthor(Guid accountId)
            => new()
            {
                account_id = accountId,
                account = MakeAccount(accountId),
                rank = null
            };

        private static story MakeStory(Guid storyId, author a, string title = "Story title")
            => new()
            {
                story_id = storyId,
                author_id = a.account_id,
                author = a,
                title = title,
                desc = "desc",
                outline = "outline",
                length_plan = "novel",
                cover_url = "cover.png",
                status = "pending",
                is_premium = false,
                created_at = DateTime.UtcNow.AddDays(-2),
                updated_at = DateTime.UtcNow.AddDays(-1),
                story_tags = new List<story_tag>()
            };

        private static language_list MakeLanguage(string code = "vi", string name = "Tiếng Việt")
            => new()
            {
                lang_id = Guid.NewGuid(),
                lang_code = code,
                lang_name = name
            };

        private static chapter MakeChapter(Guid chapterId, story s, language_list lang, string status = "pending", uint no = 1)
            => new()
            {
                chapter_id = chapterId,
                story_id = s.story_id,
                story = s,
                chapter_no = no,
                title = "Chapter 1",
                summary = "summary",
                language_id = lang.lang_id,
                language = lang,
                word_count = 1000,
                dias_price = 10,
                access_type = "free",
                content_url = "content-key",
                status = status,
                created_at = DateTime.UtcNow.AddDays(-3),
                updated_at = DateTime.UtcNow.AddDays(-1),
                submitted_at = DateTime.UtcNow.AddDays(-1),
                published_at = null,
                content_approves = new List<content_approve>()
            };

        private static content_approve MakeApproval(Guid reviewId, chapter c, decimal score, string status = "pending", DateTime? createdAt = null)
            => new()
            {
                review_id = reviewId,
                approve_type = "chapter",
                story_id = c.story_id,
                story = c.story,
                chapter_id = c.chapter_id,
                chapter = c,
                status = status,
                ai_score = score,
                ai_feedback = "ai feedback",
                moderator_feedback = null,
                moderator_id = null,
                created_at = createdAt ?? DateTime.UtcNow.AddHours(-1)
            };

        #endregion

        // ====================== ListAsync ======================

        // CASE: List – status không hợp lệ -> 400 InvalidStatus
        [Fact]
        public async Task ListAsync_Should_Throw_When_Status_Invalid()
        {
            var act = () => _svc.ListAsync("INVALID_STATUS", CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Unsupported status*");

            _repo.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // CASE: List – chỉ trả về chapter có review, dùng approval mới nhất cho mỗi chapter
        [Fact]
        public async Task ListAsync_Should_Return_Items_Using_Latest_Approval_And_Skip_Without_Approval()
        {
            var authorId = Guid.NewGuid();
            var author = MakeAuthor(authorId);
            var lang = MakeLanguage();

            var story1 = MakeStory(Guid.NewGuid(), author, "Story A");
            var chapter1 = MakeChapter(Guid.NewGuid(), story1, lang, status: "pending", no: 1);

            // 2 approval, lấy cái mới nhất
            var oldApproval = MakeApproval(Guid.NewGuid(), chapter1, 5m, "pending", DateTime.UtcNow.AddHours(-5));
            var newApproval = MakeApproval(Guid.NewGuid(), chapter1, 8m, "pending", DateTime.UtcNow.AddHours(-1));
            chapter1.content_approves = new List<content_approve> { oldApproval, newApproval };

            var story2 = MakeStory(Guid.NewGuid(), author, "Story B");
            var chapter2 = MakeChapter(Guid.NewGuid(), story2, lang, status: "pending", no: 2);
            chapter2.content_approves = new List<content_approve>(); // không có review -> skip

            _repo.Setup(r => r.GetForModerationAsync(
                            It.IsAny<IEnumerable<string>>(),
                            It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<chapter> { chapter1, chapter2 });

            var res = await _svc.ListAsync(null, CancellationToken.None);

            res.Should().HaveCount(1);
            var item = res[0];

            item.ReviewId.Should().Be(newApproval.review_id);
            item.ChapterId.Should().Be(chapter1.chapter_id);
            item.StoryId.Should().Be(story1.story_id);
            item.StoryTitle.Should().Be(story1.title);
            item.ChapterTitle.Should().Be(chapter1.title);
            item.AuthorId.Should().Be(author.account_id);
            item.AuthorUsername.Should().Be(author.account.username);
            item.AuthorEmail.Should().Be(author.account.email);
            item.LanguageCode.Should().Be(lang.lang_code);
            item.AiScore.Should().Be(8m);
            item.AiFeedback.Should().Be("ai feedback");
            item.Status.Should().Be(chapter1.status);

            _repo.VerifyAll();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // ====================== GetAsync ======================

        // CASE: Get – review không tồn tại -> 404 ModerationRequestNotFound
        [Fact]
        public async Task GetAsync_Should_Throw_When_Review_Not_Found()
        {
            var reviewId = Guid.NewGuid();

            _repo.Setup(r => r.GetContentApprovalByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((content_approve?)null);

            var act = () => _svc.GetAsync(reviewId, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Moderation request was not found*");

            _repo.VerifyAll();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // CASE: Get – approve_type != "chapter" -> 400 InvalidModerationType
        [Fact]
        public async Task GetAsync_Should_Throw_When_ApproveType_Not_Chapter()
        {
            var author = MakeAuthor(Guid.NewGuid());
            var lang = MakeLanguage();
            var story = MakeStory(Guid.NewGuid(), author);
            var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), chapter, 6m, "pending");
            approval.approve_type = "story";

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var act = () => _svc.GetAsync(approval.review_id, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Moderation request is not associated with a chapter*");

            _repo.VerifyAll();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // CASE: Get – happy path -> map ra ChapterModerationQueueItem đúng dữ liệu
        [Fact]
        public async Task GetAsync_Should_Return_Mapped_QueueItem()
        {
            var author = MakeAuthor(Guid.NewGuid());
            var lang = MakeLanguage();
            var story = MakeStory(Guid.NewGuid(), author);
            var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), chapter, 4m, "pending");

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var res = await _svc.GetAsync(approval.review_id, CancellationToken.None);

            res.ReviewId.Should().Be(approval.review_id);
            res.ChapterId.Should().Be(chapter.chapter_id);
            res.StoryId.Should().Be(story.story_id);
            res.StoryTitle.Should().Be(story.title);
            res.ChapterTitle.Should().Be(chapter.title);
            res.AuthorId.Should().Be(author.account_id);
            res.AuthorUsername.Should().Be(author.account.username);
            res.AuthorEmail.Should().Be(author.account.email);
            res.LanguageCode.Should().Be(lang.lang_code);
            res.LanguageName.Should().Be(lang.lang_name);
            res.AiScore.Should().Be(4m);
            res.AiFeedback.Should().Be("ai feedback");

            _repo.VerifyAll();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // ====================== ModerateAsync – negative paths ======================

        // CASE: Moderate – review không tồn tại -> 404 ModerationRequestNotFound
        [Fact]
        public async Task ModerateAsync_Should_Throw_When_Review_Not_Found()
        {
            var moderatorId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            var req = new ChapterModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = "OK"
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(reviewId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((content_approve?)null);

            var act = () => _svc.ModerateAsync(moderatorId, reviewId, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Moderation request was not found*");

            _repo.VerifyAll();
            _mail.VerifyNoOtherCalls();
            _noti.VerifyNoOtherCalls();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // CASE: Moderate – approve_type != "chapter" -> 400 InvalidModerationType
        [Fact]
        public async Task ModerateAsync_Should_Throw_When_ApproveType_Not_Chapter()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var lang = MakeLanguage();
            var story = MakeStory(Guid.NewGuid(), author);
            var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), chapter, 6m, "pending");
            approval.approve_type = "story";

            var req = new ChapterModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = null
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var act = () => _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Moderation request is not associated with a chapter*");

            _repo.VerifyAll();
            _mail.VerifyNoOtherCalls();
            _noti.VerifyNoOtherCalls();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // CASE: Moderate – approval.status != pending -> 400 ModerationAlreadyHandled
        [Fact]
        public async Task ModerateAsync_Should_Throw_When_Approval_Not_Pending()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var lang = MakeLanguage();
            var story = MakeStory(Guid.NewGuid(), author);
            var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), chapter, 6m, status: "approved");

            var req = new ChapterModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = null
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var act = () => _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*already been processed*");

            _repo.VerifyAll();
            _mail.VerifyNoOtherCalls();
            _noti.VerifyNoOtherCalls();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // CASE: Moderate – chapter.status != pending -> 400 ChapterNotPending
        [Fact]
        public async Task ModerateAsync_Should_Throw_When_Chapter_Not_Pending()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var lang = MakeLanguage();
            var story = MakeStory(Guid.NewGuid(), author);
            var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "published");
            var approval = MakeApproval(Guid.NewGuid(), chapter, 6m, status: "pending");

            var req = new ChapterModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = null
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var act = () => _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Chapter is not awaiting moderation*");

            _repo.VerifyAll();
            _mail.VerifyNoOtherCalls();
            _noti.VerifyNoOtherCalls();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // ====================== ModerateAsync – approve path ======================

        // CASE: Moderate – Approve=true -> chapter published, gửi mail + notification + follower notification
        [Fact]
        public async Task ModerateAsync_Should_Approve_Chapter_And_Notify_Followers()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var lang = MakeLanguage();
            var story = MakeStory(Guid.NewGuid(), author);
            var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "pending", no: 3);
            var approval = MakeApproval(Guid.NewGuid(), chapter, 7m, status: "pending");

            var req = new ChapterModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = " Looks good  "
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

            _mail.Setup(m => m.SendChapterApprovedEmailAsync(author.account.email, story.title, chapter.title))
                 .Returns(Task.CompletedTask);

            _noti.Setup(n => n.CreateAsync(
                            It.IsAny<NotificationCreateModel>(),
                            It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new NotificationResponse());

            _followers.Setup(f => f.NotifyChapterPublishedAsync(
                                author.account.account_id,
                                author.account.username,
                                story.story_id,
                                story.title,
                                chapter.chapter_id,
                                chapter.title,
                                (int)chapter.chapter_no,
                                It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

            // NEW: setup IncrementChapterDecisionAsync for approve path
            _contentModRepo.Setup(c => c.IncrementChapterDecisionAsync(
                                        moderatorId,
                                        true,
                                        It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            await _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            approval.status.Should().Be("approved");
            approval.moderator_id.Should().Be(moderatorId);
            approval.moderator_feedback.Should().Be("Looks good"); // trimmed
            chapter.status.Should().Be("published");
            chapter.published_at.Should().NotBeNull();

            _repo.VerifyAll();
            _mail.VerifyAll();
            _noti.VerifyAll();
            _followers.VerifyAll();
            _contentModRepo.VerifyAll();
        }

        // ====================== ModerateAsync – reject path ======================

        // CASE: Moderate – Approve=false -> chapter rejected, gửi mail + notification, KHÔNG notify followers
        [Fact]
        public async Task ModerateAsync_Should_Reject_Chapter_And_Not_Notify_Followers()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var lang = MakeLanguage();
            var story = MakeStory(Guid.NewGuid(), author);
            var chapter = MakeChapter(Guid.NewGuid(), story, lang, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), chapter, 6m, status: "pending");

            var req = new ChapterModerationDecisionRequest
            {
                Approve = false,
                ModeratorNote = " Not appropriate  "
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

            _mail.Setup(m => m.SendChapterRejectedEmailAsync(
                            author.account.email,
                            story.title,
                            chapter.title,
                            "Not appropriate"))
                 .Returns(Task.CompletedTask);

            _noti.Setup(n => n.CreateAsync(
                            It.IsAny<NotificationCreateModel>(),
                            It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new NotificationResponse());

            // NEW: setup IncrementChapterDecisionAsync for reject path
            _contentModRepo.Setup(c => c.IncrementChapterDecisionAsync(
                                        moderatorId,
                                        false,
                                        It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            await _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            approval.status.Should().Be("rejected");
            approval.moderator_id.Should().Be(moderatorId);
            approval.moderator_feedback.Should().Be("Not appropriate");
            approval.ai_feedback.Should().Be("ai feedback");
            chapter.status.Should().Be("rejected");
            chapter.published_at.Should().BeNull();

            _repo.VerifyAll();
            _mail.VerifyAll();
            _noti.VerifyAll();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyAll();
        }
    }
}

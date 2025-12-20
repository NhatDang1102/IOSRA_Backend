using Contract.DTOs.Request.OperationMod;   // StoryModerationDecisionRequest
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Notification;
using Contract.DTOs.Response.OperationMod;   // StoryModerationQueueItem, StoryTagResponse
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Service.Models; // NotificationCreateModel
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class StoryModerationServiceTests
    {
        private readonly Mock<IStoryModerationRepository> _repo;
        private readonly Mock<IMailSender> _mail;
        private readonly Mock<INotificationService> _noti;
        private readonly Mock<IFollowerNotificationService> _followers;
        private readonly Mock<IContentModRepository> _contentModRepo;
        private readonly StoryModerationService _svc;

        public StoryModerationServiceTests()
        {
            _repo = new Mock<IStoryModerationRepository>(MockBehavior.Strict);
            _mail = new Mock<IMailSender>(MockBehavior.Strict);
            _noti = new Mock<INotificationService>(MockBehavior.Strict);
            _followers = new Mock<IFollowerNotificationService>(MockBehavior.Strict);
            _contentModRepo = new Mock<IContentModRepository>(MockBehavior.Strict);

            _svc = new StoryModerationService(
                _repo.Object,
                _mail.Object,
                _noti.Object,
                _followers.Object,
                _contentModRepo.Object
            );
        }

        #region Helpers

        private static account MakeAuthorAccount(Guid accountId, string username = "author01", string email = "author@test.com")
            => new()
            {
                account_id = accountId,
                username = username,
                email = email,
                avatar_url = "a.png",
                status = "unbanned",
                strike = 0
            };

        private static author MakeAuthor(Guid accountId, string? rankName = "Casual")
            => new()
            {
                account_id = accountId,
                account = MakeAuthorAccount(accountId),
                rank = rankName == null
                    ? null
                    : new author_rank
                    {
                        rank_id = Guid.NewGuid(),
                        rank_name = rankName
                    }
            };

        private static story MakeStory(Guid storyId, author a, string status = "pending")
            => new()
            {
                story_id = storyId,
                author_id = a.account_id,
                author = a,
                title = "Sample story",
                desc = "desc",
                outline = "outline",
                length_plan = "novel",
                cover_url = "cover.png",
                status = status,
                is_premium = false,
                created_at = DateTime.UtcNow.AddDays(-2),
                updated_at = DateTime.UtcNow.AddDays(-1),
                story_tags = new List<story_tag>()
            };

        private static content_approve MakeApproval(Guid reviewId, story s, decimal score, string status = "pending", DateTime? createdAt = null)
            => new()
            {
                review_id = reviewId,
                approve_type = "story",
                story_id = s.story_id,
                story = s,
                chapter_id = null,
                status = status,
                ai_score = score,
                ai_feedback = "ai feedback",
                moderator_feedback = null,
                moderator_id = null,
                created_at = createdAt ?? DateTime.UtcNow.AddHours(-1)
            };

        private static story_tag MakeStoryTag(story s, Guid tagId, string tagName)
            => new()
            {
                story_id = s.story_id,
                tag_id = tagId,
                story = s,
                tag = new tag { tag_id = tagId, tag_name = tagName }
            };

        #endregion

        // ====================== ListAsync ======================

        [Fact]
        public async Task ListAsync_Should_Throw_When_Status_Invalid()
        {
            var act = () => _svc.ListAsync("INVALID_STATUS", CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Unsupported status*");

            _repo.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ListAsync_Should_Return_Items_Using_Latest_Approval_And_Skip_Stories_Without_Approval()
        {
            var authorId = Guid.NewGuid();
            var author = MakeAuthor(authorId, rankName: "VIP");

            var story1 = MakeStory(Guid.NewGuid(), author, status: "pending");
            var story2 = MakeStory(Guid.NewGuid(), author, status: "pending");

            // story1 có 2 approval, lấy cái mới nhất
            var oldApproval = MakeApproval(Guid.NewGuid(), story1, 5m, "pending", DateTime.UtcNow.AddHours(-5));
            var newApproval = MakeApproval(Guid.NewGuid(), story1, 8m, "pending", DateTime.UtcNow.AddHours(-1));
            story1.content_approves = new List<content_approve> { oldApproval, newApproval };

            // story2 không có approval -> bị bỏ qua
            story2.content_approves = new List<content_approve>();

            // gắn tags cho story1
            var tagId = Guid.NewGuid();
            var st = MakeStoryTag(story1, tagId, "Action");
            story1.story_tags.Add(st);

            _repo.Setup(r => r.GetStoriesForModerationAsync(
                            It.IsAny<IReadOnlyList<string>>(),
                            It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<story> { story1, story2 });

            var res = await _svc.ListAsync(null, CancellationToken.None);

            res.Should().HaveCount(1);
            var item = res[0];

            item.ReviewId.Should().Be(newApproval.review_id);
            item.StoryId.Should().Be(story1.story_id);
            item.Title.Should().Be(story1.title);
            item.AiScore.Should().Be(8m);
            item.AiResult.Should().Be("approved"); // ai_score > 7
            item.Tags.Should().ContainSingle(t => t.TagId == tagId && t.TagName == "Action");

            _repo.VerifyAll();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // ====================== GetAsync ======================

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

        [Fact]
        public async Task GetAsync_Should_Throw_When_ApproveType_Not_Story()
        {
            var authorId = Guid.NewGuid();
            var author = MakeAuthor(authorId);
            var story = MakeStory(Guid.NewGuid(), author);
            var approval = MakeApproval(Guid.NewGuid(), story, 6m);
            approval.approve_type = "chapter"; // sai type

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var act = () => _svc.GetAsync(approval.review_id, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Moderation request is not associated with a story*");

            _repo.VerifyAll();
            _contentModRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetAsync_Should_Return_Mapped_QueueItem()
        {
            var authorId = Guid.NewGuid();
            var author = MakeAuthor(authorId);
            var story = MakeStory(Guid.NewGuid(), author, status: "pending");

            var tagId = Guid.NewGuid();
            story.story_tags.Add(MakeStoryTag(story, tagId, "Romance"));

            var approval = MakeApproval(Guid.NewGuid(), story, 4m, status: "pending");

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var res = await _svc.GetAsync(approval.review_id, CancellationToken.None);

            res.ReviewId.Should().Be(approval.review_id);
            res.StoryId.Should().Be(story.story_id);
            res.Title.Should().Be(story.title);
            res.AuthorId.Should().Be(story.author_id);
            res.AuthorUsername.Should().Be(author.account.username);
            res.AiScore.Should().Be(4m);
            res.AiResult.Should().Be("rejected"); // score < 5
            res.Tags.Should().ContainSingle(t => t.TagName == "Romance");

            _repo.VerifyAll();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // ====================== ModerateAsync: negative paths ======================

        [Fact]
        public async Task ModerateAsync_Should_Throw_When_Review_Not_Found()
        {
            var moderatorId = Guid.NewGuid();
            var reviewId = Guid.NewGuid();
            var req = new StoryModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = "ok"
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

        [Fact]
        public async Task ModerateAsync_Should_Throw_When_Type_Not_Story()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var story = MakeStory(Guid.NewGuid(), author, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), story, 6m, status: "pending");
            approval.approve_type = "chapter";

            var req = new StoryModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = null
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var act = () => _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Moderation request is not associated with a story*");

            _repo.VerifyAll();
            _mail.VerifyNoOtherCalls();
            _noti.VerifyNoOtherCalls();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ModerateAsync_Should_Throw_When_Approval_Not_Pending()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var story = MakeStory(Guid.NewGuid(), author, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), story, 6m, status: "approved");

            var req = new StoryModerationDecisionRequest
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

        [Fact]
        public async Task ModerateAsync_Should_Throw_When_Story_Not_Pending()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid());
            var story = MakeStory(Guid.NewGuid(), author, status: "published");
            var approval = MakeApproval(Guid.NewGuid(), story, 6m, status: "pending");

            var req = new StoryModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = null
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            var act = () => _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Story is not awaiting moderation*");

            _repo.VerifyAll();
            _mail.VerifyNoOtherCalls();
            _noti.VerifyNoOtherCalls();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyNoOtherCalls();
        }

        // ====================== ModerateAsync: approve path ======================

        [Fact]
        public async Task ModerateAsync_Should_Approve_Story_And_Notify_Followers()
        {
            var moderatorId = Guid.NewGuid();
            var authorAccountId = Guid.NewGuid();
            var author = MakeAuthor(authorAccountId, rankName: "VIP");
            var story = MakeStory(Guid.NewGuid(), author, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), story, 7.5m, status: "pending");

            var req = new StoryModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = "  Good story  "
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

            _mail.Setup(m => m.SendStoryApprovedEmailAsync(author.account.email, story.title))
                 .Returns(Task.CompletedTask);

            _noti.Setup(n => n.CreateAsync(
                            It.IsAny<NotificationCreateModel>(),
                            It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new NotificationResponse());

            _followers.Setup(f => f.NotifyStoryPublishedAsync(
                                author.account.account_id,
                                author.account.username,
                                story.story_id,
                                story.title,
                                It.IsAny<CancellationToken>()))
                      .Returns(Task.CompletedTask);

            // NEW: đếm quyết định story cho mod, Approve = true
            _contentModRepo.Setup(c => c.IncrementStoryDecisionAsync(
                                        moderatorId,
                                        true,
                                        It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            await _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            approval.status.Should().Be("approved");
            approval.moderator_id.Should().Be(moderatorId);
            approval.moderator_feedback.Should().Be("Good story"); // trimmed
            story.status.Should().Be("published");
            story.is_premium.Should().BeTrue();

            _repo.VerifyAll();
            _mail.VerifyAll();
            _noti.VerifyAll();
            _followers.VerifyAll();
            _contentModRepo.VerifyAll();
        }

        // ====================== ModerateAsync: reject path ======================

        [Fact]
        public async Task ModerateAsync_Should_Reject_Story_And_Not_Notify_Followers()
        {
            var moderatorId = Guid.NewGuid();
            var author = MakeAuthor(Guid.NewGuid(), rankName: "Casual");
            var story = MakeStory(Guid.NewGuid(), author, status: "pending");
            var approval = MakeApproval(Guid.NewGuid(), story, 6m, status: "pending");

            var req = new StoryModerationDecisionRequest
            {
                Approve = false,
                ModeratorNote = "  Not suitable  "
            };

            _repo.Setup(r => r.GetContentApprovalByIdAsync(approval.review_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(approval);

            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

            _mail.Setup(m => m.SendStoryRejectedEmailAsync(
                            author.account.email,
                            story.title,
                            "Not suitable"))
                 .Returns(Task.CompletedTask);

            _noti.Setup(n => n.CreateAsync(
                            It.IsAny<NotificationCreateModel>(),
                            It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new NotificationResponse());

            // NEW: đếm quyết định story cho mod, Approve = false
            _contentModRepo.Setup(c => c.IncrementStoryDecisionAsync(
                                        moderatorId,
                                        false,
                                        It.IsAny<CancellationToken>()))
                           .Returns(Task.CompletedTask);

            await _svc.ModerateAsync(moderatorId, approval.review_id, req, CancellationToken.None);

            approval.status.Should().Be("rejected");
            approval.moderator_id.Should().Be(moderatorId);
            approval.moderator_feedback.Should().Be("Not suitable");
            story.status.Should().Be("rejected");
            story.published_at.Should().BeNull();

            _repo.VerifyAll();
            _mail.VerifyAll();
            _noti.VerifyAll();
            _followers.VerifyNoOtherCalls();
            _contentModRepo.VerifyAll();
        }
    }
}

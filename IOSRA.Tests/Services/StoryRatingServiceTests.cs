using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

using Repository.Interfaces;                 // rating, catalog, profile repos
using Repository.Entities;                   // story, author, account, reader, story_rating
using Repository.DataModels;                 // StoryRatingSummaryData
using Service.Services;                      // StoryRatingService
using Service.Interfaces;                    // INotificationService
using Contract.DTOs.Request.Story;           // StoryRatingRequest
using Contract.DTOs.Respond.Notification;    // NotificationCreateModel, NotificationResponse
using Contract.DTOs.Respond.Common;          

namespace IOSRA.Tests.Services
{
    public class StoryRatingServiceTests
    {
        private readonly Mock<IStoryRatingRepository> _ratingRepo;
        private readonly Mock<IStoryCatalogRepository> _catalogRepo;
        private readonly Mock<IProfileRepository> _profileRepo;
        private readonly Mock<INotificationService> _notify;
        private readonly StoryRatingService _svc;

        public StoryRatingServiceTests()
        {
            // Repo để Strict => mọi call chưa setup sẽ lộ ra ngay.
            _ratingRepo = new Mock<IStoryRatingRepository>(MockBehavior.Strict);
            _catalogRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
            _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);
            _notify = new Mock<INotificationService>(MockBehavior.Strict);

            _svc = new StoryRatingService(
                _ratingRepo.Object,
                _catalogRepo.Object,
                _profileRepo.Object,
                _notify.Object
            );
        }

        #region Helpers

        private static story MakePublishedStory(Guid storyId, Guid authorAccountId, string title = "S1")
        {
            return new story
            {
                story_id = storyId,
                title = title,
                status = "published",
                author_id = authorAccountId,
                author = new author
                {
                    account_id = authorAccountId,
                    account = new account
                    {
                        account_id = authorAccountId,
                        username = "author001",
                        avatar_url = "a.png",
                        email = "a@ex.com"
                    }
                }
            };
        }

        private static reader MakeReader(Guid readerId, string username = "reader001")
        {
            return new reader
            {
                account_id = readerId,
                account = new account
                {
                    account_id = readerId,
                    username = username,
                    avatar_url = "r.png",
                    email = "r@ex.com"
                }
            };
        }

        private void ArrangePublishedStory(Guid storyId, Guid authorAccountId)
        {
            _catalogRepo
                .Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakePublishedStory(storyId, authorAccountId));
        }

        private void ArrangeReader(Guid readerAccountId, string username = "reader001")
        {
            _profileRepo
                .Setup(r => r.GetReaderByIdAsync(readerAccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeReader(readerAccountId, username));
        }

        #endregion

        // CASE: thêm mới → Add + GetDetails + Notify(author)
        [Fact]
        public async Task Upsert_Should_Create_When_No_Existing_Rating_And_Notify_Author()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var readerId = Guid.NewGuid(); // khác authorId để có notify
            var req = new StoryRatingRequest { Score = 5 };

            ArrangePublishedStory(storyId, authorId);
            ArrangeReader(readerId);

            _ratingRepo.Setup(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((story_rating?)null);

            _ratingRepo.Setup(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new story_rating
                       {
                           story_id = storyId,
                           reader_id = readerId,
                           score = req.Score,
                           reader = MakeReader(readerId)
                       });

            // Notify: service gọi CreateAsync(...)
            _notify.Setup(n => n.CreateAsync(
                It.Is<NotificationCreateModel>(m =>
                    m.RecipientId == authorId &&
                    m.Type == "story_rating" &&
                    m.Title.Contains("vừa rating", StringComparison.OrdinalIgnoreCase) &&
                    m.Message.Contains($"{req.Score}/5") &&
                    m.Payload != null),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NotificationResponse
                {
                    NotificationId = Guid.NewGuid(),
                    RecipientId = authorId,
                    Type = "story_rating",
                    Title = "t",
                    Message = "m",
                    Payload = null,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

            // Act
            var item = await _svc.UpsertAsync(readerId, storyId, req, CancellationToken.None);

            // Assert
            item.Should().NotBeNull();
            item.ReaderId.Should().Be(readerId);
            item.Score.Should().Be(5);

            _ratingRepo.Verify(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.VerifyNoOtherCalls();

            _catalogRepo.VerifyAll();
            _profileRepo.VerifyAll();

            _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.Once);
            _notify.VerifyNoOtherCalls();
        }

        // CASE: đã có rating → Update + GetDetails + Notify(author)
        [Fact]
        public async Task Upsert_Should_Update_When_Existing_Rating_And_Notify_Author()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var readerId = Guid.NewGuid();
            var req = new StoryRatingRequest { Score = 4 };

            ArrangePublishedStory(storyId, authorId);
            ArrangeReader(readerId);

            _ratingRepo.Setup(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new story_rating
                       {
                           story_id = storyId,
                           reader_id = readerId,
                           score = 3
                       });

            _ratingRepo.Setup(r => r.UpdateAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new story_rating
                       {
                           story_id = storyId,
                           reader_id = readerId,
                           score = req.Score,
                           reader = MakeReader(readerId)
                       });

            _notify.Setup(n => n.CreateAsync(
                It.Is<NotificationCreateModel>(m =>
                    m.RecipientId == authorId &&
                    m.Type == "story_rating" &&
                    m.Message.Contains($"{req.Score}/5")),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new NotificationResponse
                {
                    NotificationId = Guid.NewGuid(),
                    RecipientId = authorId,
                    Type = "story_rating",
                    Title = "t",
                    Message = "m",
                    Payload = null,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

            // Act
            var item = await _svc.UpsertAsync(readerId, storyId, req, CancellationToken.None);

            // Assert
            item.Score.Should().Be(4);

            _ratingRepo.Verify(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.UpdateAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Never);
            _ratingRepo.Verify(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.VerifyNoOtherCalls();

            _catalogRepo.VerifyAll();
            _profileRepo.VerifyAll();

            _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.Once);
            _notify.VerifyNoOtherCalls();
        }

        // CASE: Get summary + page, có viewer → gắn ViewerRating; không notify
        [Fact]
        public async Task GetAsync_Should_Return_Summary_And_Page_With_ViewerRating_And_No_Notify()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var viewerId = Guid.NewGuid();

            ArrangePublishedStory(storyId, authorId);

            _ratingRepo.Setup(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((new List<story_rating>(), 0));

            var dist = new Dictionary<byte, int> { { 1, 0 }, { 2, 1 }, { 3, 2 }, { 4, 3 }, { 5, 5 } };
            _ratingRepo.Setup(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StoryRatingSummaryData(storyId, 4.6m, 11, dist));

            _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, viewerId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new story_rating
                       {
                           story_id = storyId,
                           reader_id = viewerId,
                           score = 4,
                           reader = MakeReader(viewerId, "viewer")
                       });

            // Act
            var res = await _svc.GetAsync(storyId, viewerId, page: 1, pageSize: 20, CancellationToken.None);

            // Assert
            res.StoryId.Should().Be(storyId);
            res.TotalRatings.Should().Be(11);
            res.AverageScore.Should().Be(4.6m);
            res.ViewerRating.Should().NotBeNull();
            res.ViewerRating!.Score.Should().Be(4);

            _ratingRepo.Verify(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.GetDetailsAsync(storyId, viewerId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.VerifyNoOtherCalls();

            _catalogRepo.VerifyAll();
            _notify.VerifyNoOtherCalls();
        }

        // CASE: không có rating nào → summary 0; không notify
        [Fact]
        public async Task GetAsync_Should_Return_Empty_Summary_When_No_Ratings()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var authorId = Guid.NewGuid();

            ArrangePublishedStory(storyId, authorId);

            _ratingRepo.Setup(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()))
                       .ReturnsAsync((new List<story_rating>(), 0));

            var dist = new Dictionary<byte, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
            _ratingRepo.Setup(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new StoryRatingSummaryData(storyId, 0m, 0, dist));

            // Act
            var res = await _svc.GetAsync(storyId, null, 1, 20, CancellationToken.None);

            // Assert
            res.TotalRatings.Should().Be(0);
            res.AverageScore.Should().Be(0m);
            res.Distribution[5].Should().Be(0);

            _ratingRepo.Verify(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.VerifyNoOtherCalls();

            _catalogRepo.VerifyAll();
            _notify.VerifyNoOtherCalls();
        }

        // CASE: Remove → Delete nếu có rating; không notify
        [Fact]
        public async Task Remove_Should_Delete_Existing_Rating_And_Not_Notify()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var readerId = Guid.NewGuid();

            ArrangePublishedStory(storyId, authorId);

            var existing = new story_rating
            {
                story_id = storyId,
                reader_id = readerId,
                score = 3
            };

            _ratingRepo.Setup(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                       .ReturnsAsync(existing);

            _ratingRepo.Setup(r => r.DeleteAsync(existing, It.IsAny<CancellationToken>()))
                       .Returns(Task.CompletedTask);

            // Act
            await _svc.RemoveAsync(readerId, storyId, CancellationToken.None);

            // Assert
            _ratingRepo.Verify(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.Verify(r => r.DeleteAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
            _ratingRepo.VerifyNoOtherCalls();

            _catalogRepo.VerifyAll();
            _notify.VerifyNoOtherCalls();
        }
    }
}

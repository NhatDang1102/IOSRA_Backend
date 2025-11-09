using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

using Repository.Interfaces;            // rating, catalog, profile repos
using Repository.Entities;              // story, reader, account, story_rating
using Repository.DataModels;            // StoryRatingSummaryData
using Service.Services;                 // StoryRatingService
using Contract.DTOs.Request.Story;      // StoryRatingRequest

public class StoryRatingServiceTests
{
    private readonly Mock<IStoryRatingRepository> _ratingRepo;
    private readonly Mock<IStoryCatalogRepository> _catalogRepo;
    private readonly Mock<IProfileRepository> _profileRepo;
    private readonly StoryRatingService _svc;

    public StoryRatingServiceTests()
    {
        // Strict: mọi call chưa setup sẽ lộ ra ngay
        _ratingRepo = new Mock<IStoryRatingRepository>(MockBehavior.Strict);
        _catalogRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
        _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);

        _svc = new StoryRatingService(_ratingRepo.Object, _catalogRepo.Object, _profileRepo.Object);
    }

    // Helper: khối validate chung (story published + reader tồn tại + chưa có rating)
    private void ArrangeHappyValidation(Guid storyId, Guid readerAccountId)
    {
        _catalogRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new story { story_id = storyId, status = "published" });

        _profileRepo.Setup(r => r.GetReaderByIdAsync(readerAccountId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new reader { account_id = readerAccountId });

        _ratingRepo.Setup(r => r.GetAsync(storyId, readerAccountId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((story_rating?)null);
    }

    // CASE: Thêm mới rating → Add + GetDetails
    [Fact]
    public async Task Upsert_Should_Return_Item_When_New_Rating()
    {
        var storyId = Guid.NewGuid();
        var readerId = Guid.NewGuid();

        ArrangeHappyValidation(storyId, readerId);

        _ratingRepo.Setup(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new story_rating
                   {
                       story_id = storyId,
                       reader_id = readerId,
                       score = 5,
                       reader = new reader
                       {
                           account_id = readerId,
                           account = new account { account_id = readerId, username = "reader001", avatar_url = "u.png" }
                       }
                   });

        var req = new StoryRatingRequest { Score = 5 };
        var item = await _svc.UpsertAsync(readerId, storyId, req, CancellationToken.None);

        item.Score.Should().Be(5);
        item.ReaderId.Should().Be(readerId);

        _ratingRepo.Verify(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.Verify(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.VerifyNoOtherCalls();
        _catalogRepo.VerifyAll();
        _profileRepo.VerifyAll();
    }

    // CASE: Cập nhật rating đã tồn tại → Update + GetDetails
    [Fact]
    public async Task Upsert_Should_Update_When_Rating_Already_Exists()
    {
        var storyId = Guid.NewGuid();
        var readerId = Guid.NewGuid();

        // validate chung, nhưng đã có rating
        _catalogRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new story { story_id = storyId, status = "published" });
        _profileRepo.Setup(r => r.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new reader { account_id = readerId });

        _ratingRepo.Setup(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new story_rating { story_id = storyId, reader_id = readerId, score = 3 });

        _ratingRepo.Setup(r => r.UpdateAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new story_rating
                   {
                       story_id = storyId,
                       reader_id = readerId,
                       score = 4,
                       reader = new reader { account_id = readerId, account = new account { account_id = readerId, username = "reader001" } }
                   });

        var item = await _svc.UpsertAsync(readerId, storyId, new StoryRatingRequest { Score = 4 }, CancellationToken.None);

        item.Score.Should().Be(4);

        _ratingRepo.Verify(r => r.UpdateAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Never);
        _ratingRepo.Verify(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
        _catalogRepo.VerifyAll();
        _profileRepo.VerifyAll();
    }

    // CASE: Score out-of-range → hiện tại service normalize (không throw)
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Upsert_Should_Normalize_Score_When_OutOfRange(int inputScore)
    {
        var storyId = Guid.NewGuid();
        var readerId = Guid.NewGuid();

        // validate + chưa có rating
        ArrangeHappyValidation(storyId, readerId);

        // service add rồi get details
        _ratingRepo.Setup(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        byte expected = (byte)Math.Clamp(inputScore, 1, 5);

        _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new story_rating
                   {
                       story_id = storyId,
                       reader_id = readerId,
                       score = expected,
                       reader = new reader { account_id = readerId, account = new account { account_id = readerId, username = "u" } }
                   });

        var res = await _svc.UpsertAsync(readerId, storyId, new StoryRatingRequest { Score = (byte)inputScore }, CancellationToken.None);

        res.Score.Should().Be(expected);
        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // CASE: Lấy summary + trang rating, có viewer → ViewerRating được gắn
    [Fact]
    public async Task GetAsync_Should_Return_Summary_And_Page_With_Viewer()
    {
        var storyId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();

        _catalogRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new story { story_id = storyId, status = "published" });

        // Trang rating (có thể rỗng vẫn ok)
        _ratingRepo.Setup(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<story_rating>(), 0));

        var dist = new Dictionary<byte, int> { { 1, 0 }, { 2, 1 }, { 3, 2 }, { 4, 3 }, { 5, 5 } };
        _ratingRepo.Setup(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new StoryRatingSummaryData(storyId, 4.6m, 11, dist));

        // ✅ Service dùng GetDetailsAsync để lấy ViewerRating
        _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, viewerId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new story_rating
                   {
                       story_id = storyId,
                       reader_id = viewerId,
                       score = 4,
                       reader = new reader
                       {
                           account_id = viewerId,
                           account = new account { account_id = viewerId, username = "viewer", avatar_url = "v.png" }
                       }
                   });

        var res = await _svc.GetAsync(storyId, viewerId, page: 1, pageSize: 20, CancellationToken.None);

        res.StoryId.Should().Be(storyId);
        res.TotalRatings.Should().Be(11);
        res.AverageScore.Should().Be(4.6m);
        res.ViewerRating.Should().NotBeNull();
        res.ViewerRating!.Score.Should().Be(4);

        _ratingRepo.Verify(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.Verify(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.Verify(r => r.GetDetailsAsync(storyId, viewerId, It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.VerifyNoOtherCalls();   // giờ mới an toàn

        _catalogRepo.VerifyAll();
    }

    // CASE: Không có rating nào → trả default summary
    [Fact]
    public async Task GetAsync_Should_Return_Empty_When_No_Ratings()
    {
        var storyId = Guid.NewGuid();

        _catalogRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new story { story_id = storyId, status = "published" });

        _ratingRepo.Setup(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((new List<story_rating>(), 0));

        var dist = new Dictionary<byte, int> { { 1, 0 }, { 2, 0 }, { 3, 0 }, { 4, 0 }, { 5, 0 } };
        _ratingRepo.Setup(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new StoryRatingSummaryData(storyId, 0m, 0, dist));

        var res = await _svc.GetAsync(storyId, null, 1, 20, CancellationToken.None);

        res.TotalRatings.Should().Be(0);
        res.AverageScore.Should().Be(0m);
        res.Distribution[5].Should().Be(0);
    }
}

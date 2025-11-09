using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

using Repository.Interfaces;            // IStoryRatingRepository, IStoryCatalogRepository, IProfileRepository
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
        _ratingRepo = new Mock<IStoryRatingRepository>(MockBehavior.Strict);
        _catalogRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
        _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);

        _svc = new StoryRatingService(_ratingRepo.Object, _catalogRepo.Object, _profileRepo.Object);
    }

    // helper gọn để tránh lặp code validate
    private void ArrangeHappyValidation(Guid storyId, Guid readerAccountId)
    {
        _catalogRepo
            .Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new story { story_id = storyId });

        _profileRepo
            .Setup(r => r.GetReaderByIdAsync(readerAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new reader { account_id = readerAccountId });

        // service đang gọi GetAsync trước khi quyết định add/update → cần setup
        _ratingRepo
            .Setup(r => r.GetAsync(storyId, readerAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((story_rating?)null);
    }

    [Fact]
    public async Task Upsert_Should_Return_Item_When_New_Rating()
    {
        var storyId = Guid.NewGuid();
        var readerId = Guid.NewGuid();

        ArrangeHappyValidation(storyId, readerId);

        _ratingRepo
            .Setup(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ratingRepo
            .Setup(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()))
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

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Upsert_Should_Normalize_Score_When_OutOfRange(int inputScore)
    {
        var storyId = Guid.NewGuid();
        var readerId = Guid.NewGuid();

        // expectedScore = clamp về [1..5]
        byte expectedScore = (byte)Math.Clamp(inputScore, 1, 5);

        // validate cơ bản
        _catalogRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new story { story_id = storyId });
        _profileRepo.Setup(r => r.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new reader { account_id = readerId });

        // chưa có rating
        _ratingRepo.Setup(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((story_rating?)null);

        // service hiện tại add rồi get details
        _ratingRepo.Setup(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        _ratingRepo.Setup(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new story_rating
                   {
                       story_id = storyId,
                       reader_id = readerId,
                       score = expectedScore, // giả lập normalize sau khi lưu
                       reader = new reader
                       {
                           account_id = readerId,
                           account = new account { account_id = readerId, username = "u", avatar_url = "a" }
                       }
                   });

        var req = new StoryRatingRequest { Score = (byte)inputScore };
        var res = await _svc.UpsertAsync(readerId, storyId, req, CancellationToken.None);

        res.Score.Should().Be(expectedScore);

        _ratingRepo.Verify(r => r.GetAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.Verify(r => r.AddAsync(It.IsAny<story_rating>(), It.IsAny<CancellationToken>()), Times.Once);
        _ratingRepo.Verify(r => r.GetDetailsAsync(storyId, readerId, It.IsAny<CancellationToken>()), Times.Once);
        _catalogRepo.VerifyAll();
        _profileRepo.VerifyAll();
    }


    [Fact]
    public async Task GetAsync_Should_Return_Summary()
    {
        var storyId = Guid.NewGuid();

        _catalogRepo
            .Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new story { story_id = storyId });

        _ratingRepo
            .Setup(r => r.GetRatingsPageAsync(storyId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<story_rating>(), 0));

        var dist = new Dictionary<byte, int> { { 1, 0 }, { 2, 1 }, { 3, 2 }, { 4, 3 }, { 5, 5 } };

        _ratingRepo
            .Setup(r => r.GetSummaryAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoryRatingSummaryData(storyId, 4.6m, 11, dist));

        var res = await _svc.GetAsync(storyId, viewerId: null, page: 1, pageSize: 20, CancellationToken.None);

        res.StoryId.Should().Be(storyId);
        res.TotalRatings.Should().Be(11);
        res.AverageScore.Should().Be(4.6m);
        res.Distribution[5].Should().Be(5);

        _ratingRepo.VerifyAll();
        _catalogRepo.VerifyAll();
        _profileRepo.VerifyNoOtherCalls();
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Internal;
using Contract.DTOs.Response.Profile;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Xunit;

public class PublicProfileServiceTests
{
    // Strict mock: dễ bắt nhầm call
    private readonly Mock<IPublicProfileRepository> _pubRepo;
    private readonly Mock<IAuthorFollowRepository> _followRepo;
    private readonly IMemoryCache _cache;
    private readonly PublicProfileService _svc;

    public PublicProfileServiceTests()
    {
        _pubRepo = new Mock<IPublicProfileRepository>(MockBehavior.Strict);
        _followRepo = new Mock<IAuthorFollowRepository>(MockBehavior.Strict);
        _cache = new MemoryCache(new MemoryCacheOptions());

        _svc = new PublicProfileService(_pubRepo.Object, _followRepo.Object, _cache);
    }

    // Helper: projection đầu vào từ repo public profile
    private static PublicProfileProjection MakeProjection(Guid id, bool isAuthor = true) => new PublicProfileProjection
    {
        AccountId = id,
        Username = "author01",
        Status = "active",
        AvatarUrl = "a.png",
        CreatedAt = DateTime.UtcNow.AddDays(-3),
        Bio = "bio",
        Gender = "female",
        IsAuthor = isAuthor,
        AuthorVerified = true,
        AuthorRestricted = false,
        AuthorRankName = "Casual",
        FollowerCount = 10,
        PublishedStoryCount = 3,
        LatestPublishedAt = DateTime.UtcNow.AddDays(-1)
    };

    // CASE: Get – trả đúng mapping + FollowState khi viewer đang follow
    [Fact]
    public async Task GetAsync_Should_Return_Public_Profile_With_FollowState()
    {
        var viewer = Guid.NewGuid();
        var target = Guid.NewGuid();

        // Arrange
        _pubRepo.Setup(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeProjection(target, isAuthor: true));
        _followRepo.Setup(r => r.GetAsync(viewer, target, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new follow { follower_id = viewer, followee_id = target, noti_new_story = true, created_at = DateTime.UtcNow.AddDays(-2) });

        // Act
        var res = await _svc.GetAsync(viewer, target, CancellationToken.None);

        // Assert
        res.AccountId.Should().Be(target);
        res.IsAuthor.Should().BeTrue();
        res.Author!.FollowerCount.Should().Be(10);
        res.Gender.Should().Be("F");                 // female -> F
        res.FollowState.Should().NotBeNull();        // có row follow => có state
        res.FollowState!.IsFollowing.Should().BeTrue();
        res.FollowState.NotificationsEnabled.Should().BeTrue();

        _pubRepo.VerifyAll();
        _followRepo.VerifyAll();
    }

    // CASE: Get – viewer trống hoặc không phải author => không tra follow
    [Fact]
    public async Task GetAsync_Should_Skip_Follow_When_Viewer_Empty_Or_Target_NotAuthor()
    {
        var target = Guid.NewGuid();

        _pubRepo.Setup(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeProjection(target, isAuthor: false));

        var res = await _svc.GetAsync(Guid.Empty, target, CancellationToken.None);

        res.IsAuthor.Should().BeFalse();
        res.FollowState.Should().BeNull(); // không check follow

        _pubRepo.VerifyAll();
        _followRepo.VerifyNoOtherCalls();
    }

    // CASE: Get – dùng cache (lần 2 không hit repo)
    [Fact]
    public async Task GetAsync_Should_Use_Cache_On_Subsequent_Calls()
    {
        var target = Guid.NewGuid();

        _pubRepo.Setup(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeProjection(target));

        var first = await _svc.GetAsync(Guid.Empty, target, CancellationToken.None);
        first.Username.Should().Be("author01");

        // Lần 2: không setup thêm => nếu repo bị gọi sẽ fail VerifyAll()
        var second = await _svc.GetAsync(Guid.Empty, target, CancellationToken.None);
        second.AccountId.Should().Be(target);

        _pubRepo.Verify(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()), Times.Once);
        _followRepo.VerifyNoOtherCalls();
    }

    // CASE: Get – target rỗng => 400
    [Fact]
    public async Task GetAsync_Should_Throw_When_Target_Empty()
    {
        var act = () => _svc.GetAsync(Guid.NewGuid(), Guid.Empty, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Target account id is required*");

        _pubRepo.VerifyNoOtherCalls();
        _followRepo.VerifyNoOtherCalls();
    }

    // CASE: Get – account banned => 404
    [Fact]
    public async Task GetAsync_Should_Throw_When_Target_Banned()
    {
        var target = Guid.NewGuid();
        var banned = MakeProjection(target);
        banned.Status = "banned";

        _pubRepo.Setup(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()))
                .ReturnsAsync(banned);

        var act = () => _svc.GetAsync(Guid.Empty, target, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Account is not available*");

        _pubRepo.VerifyAll();
        _followRepo.VerifyNoOtherCalls();
    }

    // CASE: Get – repo trả null (không tìm thấy)
    [Fact]
    public async Task GetAsync_Should_Throw_When_Profile_NotFound()
    {
        var target = Guid.NewGuid();

        _pubRepo.Setup(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()))
                .ReturnsAsync((PublicProfileProjection?)null);

        var act = () => _svc.GetAsync(Guid.Empty, target, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Account was not found*");

        _pubRepo.VerifyAll();
        _followRepo.VerifyNoOtherCalls();
    }

    // CASE: Get – viewer KHÔNG follow => FollowState = null
    [Fact]
    public async Task GetAsync_Should_Return_Null_FollowState_When_No_Follow_Row()
    {
        var viewer = Guid.NewGuid();
        var target = Guid.NewGuid();

        _pubRepo.Setup(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeProjection(target, isAuthor: true));
        _followRepo.Setup(r => r.GetAsync(viewer, target, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((follow?)null);

        var res = await _svc.GetAsync(viewer, target, CancellationToken.None);

        res.IsAuthor.Should().BeTrue();
        res.FollowState.Should().BeNull(); // service chỉ set khi có row follow

        _pubRepo.VerifyAll();
        _followRepo.VerifyAll();
    }

    // CASE: Get – gender 'other' / 'unspecified' giữ nguyên, không rút gọn
    [Theory]
    [InlineData("other")]
    [InlineData("unspecified")]
    public async Task GetAsync_Should_PassThrough_Gender_Other_And_Unspecified(string stored)
    {
        var target = Guid.NewGuid();
        var proj = MakeProjection(target);
        proj.Gender = stored;

        _pubRepo.Setup(r => r.GetPublicProfileAsync(target, It.IsAny<CancellationToken>()))
                .ReturnsAsync(proj);

        var res = await _svc.GetAsync(Guid.Empty, target, CancellationToken.None);

        res.Gender.Should().Be(stored);

        _pubRepo.VerifyAll();
        _followRepo.VerifyNoOtherCalls();
    }
}

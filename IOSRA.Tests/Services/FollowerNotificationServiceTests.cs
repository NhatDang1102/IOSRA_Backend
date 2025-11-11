using FluentAssertions;
using Moq;
using Repository.Interfaces;
using Service.Constants;
using Service.Interfaces;
using Service.Services; // FollowerNotificationService
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class FollowerNotificationServiceTests
{
    private readonly Mock<IAuthorFollowRepository> _followRepo;
    private readonly Mock<INotificationService> _notify;
    private readonly FollowerNotificationService _svc;

    public FollowerNotificationServiceTests()
    {
        _followRepo = new Mock<IAuthorFollowRepository>(MockBehavior.Strict);
        _notify = new Mock<INotificationService>(MockBehavior.Strict);
        _svc = new FollowerNotificationService(_followRepo.Object, _notify.Object);
    }

    // CASE: StoryPublished – chỉ gửi cho các follower trong danh sách repo trả về
    [Fact]
    public async Task NotifyStoryPublishedAsync_Should_Notify_All_Followers_From_Repo()
    {
        var authorId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var authorName = "Author A";
        var storyTitle = "Cool Story";

        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();
        _followRepo.Setup(r => r.GetFollowerIdsForNotificationsAsync(authorId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<Guid> { f1, f2 });

        // Expect: tạo noti cho từng follower
        var captured = new System.Collections.Generic.List<NotificationCreateModel>();

        _notify.Setup(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()))
              .Callback<NotificationCreateModel, CancellationToken>((m, _) => captured.Add(m))
              .ReturnsAsync(new Contract.DTOs.Respond.Notification.NotificationResponse());

        await _svc.NotifyStoryPublishedAsync(authorId, authorName, storyId, storyTitle, CancellationToken.None);

        _followRepo.VerifyAll();
        _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));

        // Assert sau khi chạy
        captured.Should().NotBeNull();
        captured.Count.Should().BeGreaterThanOrEqualTo(2);
        captured.Select(m => m.RecipientId).Should().Contain(new[] { f1, f2 });
        captured.All(m => m.Type == NotificationTypes.NewStory).Should().BeTrue();
        captured.All(m => m.Title.Contains(authorName) && m.Message.Contains(storyTitle)).Should().BeTrue();
        // kiểm tra payload có khóa cần thiết
        foreach (var m in captured)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(m.Payload, new System.Text.Json.JsonSerializerOptions());
            json.Should().Contain("\"authorId\"").And.Contain("\"storyId\"");
        }

        _followRepo.VerifyAll();
        _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // CASE: ChapterPublished – gửi cho tất cả follower; payload đủ authorId/storyId/chapterId/chapterNo
    [Fact]
    public async Task NotifyChapterPublishedAsync_Should_Notify_All_And_Include_Chapter_Payload()
    {
        var authorId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var authorName = "Author B";
        var storyTitle = "Another Story";
        var chapterNo = 12;
        var chapterTitle = "Twist";

        var f1 = Guid.NewGuid();
        _followRepo.Setup(r => r.GetFollowerIdsForNotificationsAsync(authorId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new List<Guid> { f1 });

        var captured = new System.Collections.Generic.List<NotificationCreateModel>();

        _notify.Setup(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()))
              .Callback<NotificationCreateModel, CancellationToken>((m, _) => captured.Add(m))
              .ReturnsAsync(new Contract.DTOs.Respond.Notification.NotificationResponse());

        await _svc.NotifyChapterPublishedAsync(
            authorId, authorName, storyId, storyTitle, chapterId, chapterTitle, chapterNo, CancellationToken.None);

        _followRepo.VerifyAll();
        _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Assert sau khi chạy
        captured.Should().NotBeEmpty();
        captured.Any(m => m.RecipientId == f1 && m.Type == NotificationTypes.NewChapter).Should().BeTrue();

        var m0 = captured.First(m => m.RecipientId == f1 && m.Type == NotificationTypes.NewChapter);
        m0.Title.Should().Contain(authorName);
        m0.Message.Should().Contain($"Chương {chapterNo}").And.Contain(chapterTitle).And.Contain(storyTitle);

        var json = System.Text.Json.JsonSerializer.Serialize(m0.Payload, new System.Text.Json.JsonSerializerOptions());
        json.Should().Contain("\"authorId\"").And.Contain("\"storyId\"").And.Contain("\"chapterId\"").And.Contain("\"chapterNo\"");


        _followRepo.VerifyAll();
        _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // CASE: Không có follower -> không gửi
    [Fact]
    public async Task NotifyStoryPublishedAsync_Should_Not_Notify_When_No_Followers()
    {
        var authorId = Guid.NewGuid();
        var storyId = Guid.NewGuid();

        _followRepo.Setup(r => r.GetFollowerIdsForNotificationsAsync(authorId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(Array.Empty<Guid>());

        await _svc.NotifyStoryPublishedAsync(authorId, "X", storyId, "Y", CancellationToken.None);

        _followRepo.VerifyAll();
        _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // CASE: Repo lỗi -> bubble, không gọi notification
    [Fact]
    public async Task NotifyChapterPublishedAsync_Should_Bubble_When_Repo_Fails()
    {
        var authorId = Guid.NewGuid();

        _followRepo.Setup(r => r.GetFollowerIdsForNotificationsAsync(authorId, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new InvalidOperationException("repo failed"));

        var act = () => _svc.NotifyChapterPublishedAsync(
            authorId, "N1", Guid.NewGuid(), "S1", Guid.NewGuid(), "C1", 1, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*repo failed*");

        _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.Never);
        _followRepo.VerifyAll();
    }
}

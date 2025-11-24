using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Services;        // NotificationService
using Service.Interfaces;      // INotificationDispatcher
using Xunit;
using Contract.DTOs.Response.Notification;

public class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _repo;
    private readonly Mock<INotificationDispatcher> _dispatcher;
    private readonly NotificationService _svc;

    public NotificationServiceTests()
    {
        _repo = new Mock<INotificationRepository>(MockBehavior.Strict);
        _dispatcher = new Mock<INotificationDispatcher>(MockBehavior.Strict);
        _svc = new NotificationService(_repo.Object, _dispatcher.Object);
    }

    // CASE: Create – lưu entity + payload camelCase + dispatch 1 lần
    [Fact]
    public async Task CreateAsync_Should_Save_And_Dispatch_With_CamelCase_Payload()
    {
        var recipient = Guid.NewGuid();
        var storyId = Guid.NewGuid();

        // Positional record (không dùng named params)
        var model = new NotificationCreateModel(
            recipient,
            "ChapterComment",
            "Có bình luận mới",
            "reader001 vừa bình luận",
            new { StoryId = storyId, ChapterNo = 7 } // -> "storyId","chapterNo"
        );

        // Bắt entity Add và return y nguyên (repo trả Task<notification>)
        notification? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<notification>(), It.IsAny<CancellationToken>()))
             .Callback<notification, CancellationToken>((n, _) => saved = n)
             .ReturnsAsync(() => saved!);

        // Dispatcher trong repo của bạn chỉ có 1 tham số
        _dispatcher.Setup(d => d.DispatchAsync(
        It.Is<NotificationResponse>(r =>
            r.RecipientId == recipient
            && r.Type == "ChapterComment"
            && r.Title == "Có bình luận mới"
            && r.Message == "reader001 vừa bình luận"
            && r.IsRead == false
            && r.Payload != null
            && JsonSerializer.Serialize(r.Payload, new JsonSerializerOptions()).Contains("\"storyId\"")
            && JsonSerializer.Serialize(r.Payload, new JsonSerializerOptions()).Contains("\"chapterNo\"")
        )))
    .Returns(Task.CompletedTask);

        var res = await _svc.CreateAsync(model, CancellationToken.None);

        // result mapping
        res.RecipientId.Should().Be(recipient);
        res.Type.Should().Be("ChapterComment");
        res.IsRead.Should().BeFalse();
        var payloadJson = JsonSerializer.Serialize(res.Payload, new JsonSerializerOptions());
        payloadJson.Should().Contain("\"storyId\"").And.Contain("\"chapterNo\"");

        // entity được lưu
        saved.Should().NotBeNull();
        saved!.recipient_id.Should().Be(recipient);
        saved.is_read.Should().BeFalse();
        saved.notification_id.Should().NotBeEmpty();

        _repo.VerifyAll();
        _dispatcher.VerifyAll();
    }

    // CASE: Create – payload null vẫn lưu & dispatch
    [Fact]
    public async Task CreateAsync_Should_Allow_Null_Payload()
    {
        var recipient = Guid.NewGuid();

        var model = new NotificationCreateModel(
            recipient,
            "System",
            "Chào mừng",
            "Tài khoản của bạn đã được tạo.",
            null
        );

        _repo.Setup(r => r.AddAsync(It.Is<notification>(n =>
                        n.recipient_id == recipient &&
                        n.type == "System" &&
                        n.title == "Chào mừng" &&
                        n.message == "Tài khoản của bạn đã được tạo." &&
                        n.is_read == false),
                    It.IsAny<CancellationToken>()))
             .ReturnsAsync((notification n, CancellationToken _) => n);

        _dispatcher.Setup(d => d.DispatchAsync(
                It.Is<NotificationResponse>(r =>
                    r.RecipientId == recipient &&
                    r.Type == "System" &&
                    r.Payload == null &&
                    r.IsRead == false)))
            .Returns(Task.CompletedTask);

        var res = await _svc.CreateAsync(model, CancellationToken.None);

        res.Payload.Should().BeNull();
        res.IsRead.Should().BeFalse();

        _repo.VerifyAll();
        _dispatcher.VerifyAll();
    }

    // CASE: Create – repo AddAsync lỗi => bubble, KHÔNG dispatch
    [Fact]
    public async Task CreateAsync_Should_Bubble_When_Repo_Fails_And_Not_Dispatch()
    {
        var recipient = Guid.NewGuid();
        var model = new NotificationCreateModel(
            recipient,
            "System",
            "T",
            "M",
            new { Foo = 1 }
        );

        _repo.Setup(r => r.AddAsync(It.IsAny<notification>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => _svc.CreateAsync(model, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*db down*");

        // Không được gọi dispatcher khi lưu DB thất bại
        _dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationResponse>()), Times.Never);
        _repo.VerifyAll();
    }

    // CASE: Create – dispatcher lỗi => bubble, nhưng repo đã lưu
    [Fact]
    public async Task CreateAsync_Should_Bubble_When_Dispatch_Fails_After_Saving()
    {
        var recipient = Guid.NewGuid();
        var model = new NotificationCreateModel(
            recipient,
            "System",
            "T",
            "M",
            null
        );

        // Repo lưu OK và trả về entity
        _repo.Setup(r => r.AddAsync(It.IsAny<notification>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((notification n, CancellationToken _) => n);

        // Dispatcher quăng lỗi
        _dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationResponse>()))
                   .ThrowsAsync(new ApplicationException("push failed"));

        var act = () => _svc.CreateAsync(model, CancellationToken.None);

        await act.Should().ThrowAsync<ApplicationException>()
                 .WithMessage("*push failed*");

        // Repo chắc chắn đã được gọi 1 lần
        _repo.Verify(r => r.AddAsync(It.IsAny<notification>(), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.VerifyAll();
    }

    // CASE: Get – normalize paging & map items
    [Fact]
    public async Task GetAsync_Should_Normalize_Paging_And_Map_Items()
    {
        var recipient = Guid.NewGuid();

        var n1 = new notification
        {
            notification_id = Guid.NewGuid(),
            recipient_id = recipient,
            type = "System",
            title = "Hi",
            message = "Welcome",
            payload = null,
            is_read = false,
            created_at = DateTime.UtcNow.AddMinutes(-10)
        };
        var n2 = new notification
        {
            notification_id = Guid.NewGuid(),
            recipient_id = recipient,
            type = "ChapterComment",
            title = "New comment",
            message = "reader001 ...",
            // payload là JSON hợp lệ
            payload = JsonSerializer.Serialize(new { StoryId = Guid.NewGuid(), ChapterNo = 5 }),
            is_read = true,
            created_at = DateTime.UtcNow.AddMinutes(-5)
        };

        // page<=0 -> 1 ; size<=0 -> 20
        _repo.Setup(r => r.GetAsync(recipient, 1, 20, It.IsAny<CancellationToken>()))
             .ReturnsAsync((new[] { n1, n2 }, 2));

        var res = await _svc.GetAsync(recipient, page: 0, pageSize: 0, CancellationToken.None);

        res.Page.Should().Be(1);
        res.PageSize.Should().Be(20);
        res.Total.Should().Be(2);
        res.Items.Should().HaveCount(2);
        res.Items[0].Type.Should().Be("System");
        res.Items[1].IsRead.Should().BeTrue();
        // payload json -> object có key camelCase
        JsonSerializer.Serialize(res.Items[1].Payload, new JsonSerializerOptions())
            .Should().Contain("\"StoryId\"").And.Contain("\"ChapterNo\"");

        _repo.VerifyAll();
        _dispatcher.VerifyNoOtherCalls();
    }

    // CASE: Get – clamp pageSize về 100 khi quá lớn
    [Fact]
    public async Task GetAsync_Should_Clamp_PageSize_To_100()
    {
        var recipient = Guid.NewGuid();

        _repo.Setup(r => r.GetAsync(recipient, 2, 100, It.IsAny<CancellationToken>()))
             .ReturnsAsync((Array.Empty<notification>(), 0));

        var res = await _svc.GetAsync(recipient, page: 2, pageSize: 500, CancellationToken.None);

        res.Page.Should().Be(2);
        res.PageSize.Should().Be(100);

        _repo.VerifyAll();
        _dispatcher.VerifyNoOtherCalls();
    }

    // CASE: Get – legacy payload là plain string -> pass-through (không parse)
    [Fact]
    public async Task GetAsync_Should_PassThrough_PlainString_Payload()
    {
        var recipient = Guid.NewGuid();
        var n = new notification
        {
            notification_id = Guid.NewGuid(),
            recipient_id = recipient,
            type = "System",
            title = "Legacy",
            message = "legacy payload",
            payload = "not-json", // chuỗi thường
            is_read = false,
            created_at = DateTime.UtcNow
        };

        _repo.Setup(r => r.GetAsync(recipient, 1, 20, It.IsAny<CancellationToken>()))
             .ReturnsAsync((new[] { n }, 1));

        var res = await _svc.GetAsync(recipient, page: 1, pageSize: 20, CancellationToken.None);

        res.Items.Should().HaveCount(1);
        res.Items[0].Payload.Should().Be("not-json");

        _repo.VerifyAll();
    }

    // CASE: MarkRead / MarkAllRead – chỉ cần gọi repo đúng 1 lần
    [Fact]
    public async Task MarkReadAsync_Should_Delegate_To_Repo()
    {
        var recipient = Guid.NewGuid();
        var notiId = Guid.NewGuid();

        _repo.Setup(r => r.MarkReadAsync(recipient, notiId, It.IsAny<CancellationToken>()))
             .ReturnsAsync(1);

        await _svc.MarkReadAsync(recipient, notiId, CancellationToken.None);

        _repo.VerifyAll();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MarkAllReadAsync_Should_Delegate_To_Repo()
    {
        var recipient = Guid.NewGuid();

        _repo.Setup(r => r.MarkAllReadAsync(recipient, It.IsAny<CancellationToken>()))
             .ReturnsAsync(3);

        await _svc.MarkAllReadAsync(recipient, CancellationToken.None);

        _repo.VerifyAll();
        _dispatcher.VerifyNoOtherCalls();
    }
}

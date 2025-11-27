using Contract.DTOs.Response.Chapter;        // ChapterCommentResponse, StoryCommentFeedResponse
using Contract.DTOs.Response.Common;         // PagedResult<T>
using Contract.DTOs.Response.Notification;   // NotificationCreateModel, NotificationResponse
using FluentAssertions;
using Moq;
using Repository.DataModels;
using Repository.Entities;                  // chapter, story, chapter_comment, reader, author, account
using Repository.Interfaces;                // IChapterCommentRepository, IStoryCatalogRepository, IProfileRepository
using Service.Constants;                    // NotificationTypes
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;                     // ChapterCommentService
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class ChapterCommentServiceTests
{
    private readonly Mock<IChapterCommentRepository> _commentRepo;
    private readonly Mock<IStoryCatalogRepository> _storyRepo;
    private readonly Mock<IProfileRepository> _profileRepo;
    private readonly Mock<INotificationService> _notify;
    private readonly ChapterCommentService _svc;

    public ChapterCommentServiceTests()
    {
        // Strict: mọi call chưa setup sẽ lộ ra ngay
        _commentRepo = new Mock<IChapterCommentRepository>(MockBehavior.Strict);
        _storyRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
        _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);
        _notify = new Mock<INotificationService>(MockBehavior.Strict);

        _svc = new ChapterCommentService(_commentRepo.Object, _storyRepo.Object, _profileRepo.Object, _notify.Object);
    }

    // Helper: tạo 1 comment đủ navigation (reader.account + chapter.story) để mapper không null
    private static chapter_comment MakeComment(Guid storyId, Guid chapterId, int chapterNo, string chapterTitle,
                                               Guid readerId, string username, string content) =>
        new chapter_comment
        {
            comment_id = Guid.NewGuid(),
            story_id = storyId,
            chapter_id = chapterId,
            content = content,
            status = "visible",
            is_locked = false,
            created_at = DateTime.UtcNow.AddMinutes(-5),
            updated_at = DateTime.UtcNow,
            reader_id = readerId,
            reader = new reader
            {
                account_id = readerId,
                account = new account { account_id = readerId, username = username, avatar_url = "av.png" }
            },
            chapter = new chapter
            {
                chapter_id = chapterId,
                story_id = storyId,
                chapter_no = (uint)chapterNo,
                title = chapterTitle,
                status = "published",
                story = new story { story_id = storyId, status = "published" }
            }
        };

    // Helper: chapter published + story published + author (để Notify hoạt động)
    private static chapter MakePublishedChapterWithAuthor(Guid storyId, Guid chapterId, uint chapterNo, string title, Guid authorId) =>
        new chapter
        {
            chapter_id = chapterId,
            story_id = storyId,
            chapter_no = chapterNo,
            title = title,
            status = "published",
            story = new story
            {
                story_id = storyId,
                status = "published",
                author_id = authorId,
                author = new author
                {
                    account_id = authorId,
                    account = new account { account_id = authorId, username = "author001", avatar_url = "a.png", email = "a@ex.com" }
                }
            }
        };

    // Helper: reader profile (để lấy username đưa vào title/message notify)
    private static reader MakeReader(Guid readerId, string username) =>
        new reader
        {
            account_id = readerId,
            account = new account { account_id = readerId, username = username, avatar_url = "r.png", email = "r@ex.com" }
        };

    // CASE: Happy path – lấy comment theo CHAPTER, có phân trang (KHÔNG notify)
    [Fact]
    public async Task GetByChapterAsync_Should_Return_Paged_Public_Comments()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new chapter
                    {
                        chapter_id = chapterId,
                        story_id = storyId,
                        status = "published",
                        story = new story { story_id = storyId, status = "published" }
                    });

        var c1 = MakeComment(storyId, chapterId, 1, "Ch1", Guid.NewGuid(), "u1", "A");
        var c2 = MakeComment(storyId, chapterId, 1, "Ch1", Guid.NewGuid(), "u2", "B");

        _commentRepo.Setup(r => r.GetByChapterAsync(chapterId, 2, 2, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((new List<chapter_comment> { c1, c2 }, 8));

        _commentRepo.Setup(r => r.GetReactionAggregatesAsync(
            It.IsAny<Guid[]>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Dictionary<Guid, ChapterCommentReactionAggregate>());

        var res = await _svc.GetByChapterAsync(chapterId, page: 2, pageSize: 2, CancellationToken.None);

        res.Items.Should().HaveCount(2);
        res.Total.Should().Be(8);
        res.Page.Should().Be(2);
        res.PageSize.Should().Be(2);
        res.Items.Select(x => x.ChapterId).Distinct().Single().Should().Be(chapterId);
        res.Items.Select(x => x.Username).Should().BeEquivalentTo(new[] { "u1", "u2" });

        _commentRepo.VerifyAll();
        _storyRepo.VerifyNoOtherCalls();
        _profileRepo.VerifyNoOtherCalls();
        _notify.VerifyNoOtherCalls(); // không notify khi GET
    }

    // CASE: Lỗi – chapter không tồn tại (KHÔNG notify)
    [Fact]
    public async Task GetByChapterAsync_Should_Throw_When_Chapter_NotFound()
    {
        var chapterId = Guid.NewGuid();
        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((chapter?)null);

        var act = () => _svc.GetByChapterAsync(chapterId, 1, 10, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Chapter was not found*");

        _commentRepo.VerifyAll();
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Lỗi – chapter chưa publish (KHÔNG notify)
    [Fact]
    public async Task GetByChapterAsync_Should_Throw_When_Chapter_NotPublished()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new chapter
                    {
                        chapter_id = chapterId,
                        story_id = storyId,
                        status = "draft",
                        story = new story { story_id = storyId, status = "published" }
                    });

        var act = () => _svc.GetByChapterAsync(chapterId, 1, 20, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Comments are only allowed on published chapters*");

        _commentRepo.VerifyAll();
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Lỗi – story chưa publish (KHÔNG notify)
    [Fact]
    public async Task GetByChapterAsync_Should_Throw_When_Story_NotPublished()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new chapter
                    {
                        chapter_id = chapterId,
                        story_id = storyId,
                        status = "published",
                        story = new story { story_id = storyId, status = "draft" }
                    });

        var act = () => _svc.GetByChapterAsync(chapterId, 1, 20, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*when the story is published*");

        _commentRepo.VerifyAll();
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Happy path – lấy FEED theo STORY, có filter (KHÔNG notify)
    [Fact]
    public async Task GetByStoryAsync_Should_Return_Feed_With_Mapped_Items_And_Filter()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new story { story_id = storyId, status = "published" });

        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new chapter
                    {
                        chapter_id = chapterId,
                        story_id = storyId,
                        status = "published",
                        story = new story { story_id = storyId, status = "published" }
                    });

        var c1 = MakeComment(storyId, chapterId, 5, "Ch5", Guid.NewGuid(), "u3", "X");
        var c2 = MakeComment(storyId, chapterId, 5, "Ch5", Guid.NewGuid(), "u4", "Y");

        _commentRepo.Setup(r => r.GetByStoryAsync(storyId, chapterId, 1, 10, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((new List<chapter_comment> { c1, c2 }, 2));

        _commentRepo.Setup(r => r.GetReactionAggregatesAsync(
            It.IsAny<Guid[]>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Dictionary<Guid, ChapterCommentReactionAggregate>());

        var res = await _svc.GetByStoryAsync(storyId, chapterId, page: 1, pageSize: 10, CancellationToken.None);

        res.StoryId.Should().Be(storyId);
        res.ChapterFilterId.Should().Be(chapterId);
        res.Comments.Items.Should().HaveCount(2);
        res.Comments.Total.Should().Be(2);
        res.Comments.Items.All(i => i.ChapterNo == 5 && i.ChapterTitle == "Ch5").Should().BeTrue();

        _commentRepo.VerifyAll();
        _storyRepo.VerifyAll();
        _profileRepo.VerifyNoOtherCalls();
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Lỗi – filter chapter không tồn tại (KHÔNG notify)
    [Fact]
    public async Task GetByStoryAsync_Should_Throw_When_Chapter_Filter_NotFound()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new story { story_id = storyId, status = "published" });

        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((chapter?)null);

        var act = () => _svc.GetByStoryAsync(storyId, chapterId, 1, 10, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Chapter was not found*");

        _storyRepo.VerifyAll();
        _commentRepo.VerifyAll();
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Lỗi – filter chapter không thuộc story (KHÔNG notify)
    [Fact]
    public async Task GetByStoryAsync_Should_Throw_When_Chapter_Not_In_Story()
    {
        var storyId = Guid.NewGuid();
        var otherStoryId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new story { story_id = storyId, status = "published" });

        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new chapter
                    {
                        chapter_id = chapterId,
                        story_id = otherStoryId,
                        status = "published",
                        story = new story { story_id = otherStoryId, status = "published" }
                    });

        var act = () => _svc.GetByStoryAsync(storyId, chapterId, 1, 10, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Chapter does not belong to the requested story*");

        _storyRepo.VerifyAll();
        _commentRepo.VerifyAll();
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Happy path – lấy FEED theo STORY, KHÔNG filter (KHÔNG notify)
    [Fact]
    public async Task GetByStoryAsync_Should_Work_Without_Chapter_Filter()
    {
        var storyId = Guid.NewGuid();

        _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new story { story_id = storyId, status = "published" });

        var c1 = MakeComment(storyId, Guid.NewGuid(), 2, "Ch2", Guid.NewGuid(), "uA", "cmt");

        _commentRepo.Setup(r => r.GetByStoryAsync(storyId, null, 3, 5, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((new List<chapter_comment> { c1 }, 1));

        _commentRepo.Setup(r => r.GetReactionAggregatesAsync(
            It.IsAny<Guid[]>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new Dictionary<Guid, ChapterCommentReactionAggregate>());

        var res = await _svc.GetByStoryAsync(storyId, chapterId: null, page: 3, pageSize: 5, CancellationToken.None);

        res.StoryId.Should().Be(storyId);
        res.ChapterFilterId.Should().BeNull();
        res.Comments.Items.Should().HaveCount(1);
        res.Comments.Page.Should().Be(3);
        res.Comments.PageSize.Should().Be(5);

        _storyRepo.VerifyAll();
        _commentRepo.VerifyAll();
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Create – tạo mới → Add + Get lại + Notify(author)
    [Fact]
    public async Task Create_Should_Add_And_Notify_Author()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var readerId = Guid.NewGuid(); // khác author để có notify

        var chapter = MakePublishedChapterWithAuthor(storyId, chapterId, 7, "Ch7", authorId);
        var reader = MakeReader(readerId, "reader001");

        _profileRepo.Setup(r => r.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>())).ReturnsAsync(reader);
        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);

        // Add -> Get lại (để map public response)
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<chapter_comment>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

        // Mô phỏng bản "saved" với navigation đầy đủ (reader.account + chapter.story)
        chapter_comment? saved = null;
        _commentRepo.Setup(r => r.GetAsync(chapterId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => saved!); // trả bản saved đã dựng

        // Khi AddAsync được gọi, ta bắt entity để gán cho "saved" (giữ nguyên id/payload)
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<chapter_comment>(), It.IsAny<CancellationToken>()))
                    .Callback<chapter_comment, CancellationToken>((c, _) => saved = new chapter_comment
                    {
                        comment_id = c.comment_id,
                        story_id = storyId,
                        chapter_id = chapterId,
                        content = "hello",
                        status = "visible",
                        is_locked = false,
                        created_at = c.created_at,
                        updated_at = c.updated_at,
                        reader_id = readerId,
                        reader = reader,
                        chapter = chapter
                    })
                    .Returns(Task.CompletedTask);

        // Expect notify 1 lần tới tác giả
        _notify.Setup(n => n.CreateAsync(
                It.Is<NotificationCreateModel>(m =>
                    m.RecipientId == authorId &&
                    m.Type == NotificationTypes.ChapterComment &&
                    m.Title.Contains("vừa bình luận", StringComparison.OrdinalIgnoreCase) &&
                    m.Message.Contains("reader001") &&
                    m.Payload != null),
                It.IsAny<CancellationToken>()))
               .ReturnsAsync(new NotificationResponse
               {
                   NotificationId = Guid.NewGuid(),
                   RecipientId = authorId,
                   Type = NotificationTypes.ChapterComment,
                   Title = "t",
                   Message = "m",
                   Payload = null,
                   IsRead = false,
                   CreatedAt = DateTime.UtcNow
               });

        var req = new Contract.DTOs.Request.Chapter.ChapterCommentCreateRequest { Content = "hello" };
        var res = await _svc.CreateAsync(readerId, chapterId, req, CancellationToken.None);

        res.Should().NotBeNull();
        res.Content.Should().Be("hello");
        res.ChapterId.Should().Be(chapterId);

        _profileRepo.Verify(r => r.GetReaderByIdAsync(readerId, It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.AddAsync(It.IsAny<chapter_comment>(), It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.GetAsync(chapterId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.VerifyNoOtherCalls();

        _notify.Verify(n => n.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.Once);
        _notify.VerifyNoOtherCalls();
    }

    // CASE: Create – commenter là author ⇒ KHÔNG notify
    [Fact]
    public async Task Create_Should_Not_Notify_When_Commenter_Is_Author()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var authorId = Guid.NewGuid(); // trùng reader

        var chapter = MakePublishedChapterWithAuthor(storyId, chapterId, 3, "Ch3", authorId);
        var reader = MakeReader(authorId, "author001");

        _profileRepo.Setup(r => r.GetReaderByIdAsync(authorId, It.IsAny<CancellationToken>())).ReturnsAsync(reader);
        _commentRepo.Setup(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>())).ReturnsAsync(chapter);

        // Add + Get saved
        chapter_comment? saved = null;
        _commentRepo.Setup(r => r.AddAsync(It.IsAny<chapter_comment>(), It.IsAny<CancellationToken>()))
                    .Callback<chapter_comment, CancellationToken>((c, _) => saved = new chapter_comment
                    {
                        comment_id = c.comment_id,
                        story_id = storyId,
                        chapter_id = chapterId,
                        content = "self-comment",
                        status = "visible",
                        is_locked = false,
                        created_at = c.created_at,
                        updated_at = c.updated_at,
                        reader_id = authorId,
                        reader = reader,
                        chapter = chapter
                    })
                    .Returns(Task.CompletedTask);

        _commentRepo.Setup(r => r.GetAsync(chapterId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => saved!);

        var req = new Contract.DTOs.Request.Chapter.ChapterCommentCreateRequest { Content = "self-comment" };
        var res = await _svc.CreateAsync(authorId, chapterId, req, CancellationToken.None);

        res.Content.Should().Be("self-comment");
        _notify.VerifyNoOtherCalls(); // không notify

        // repo/profile verify đầy đủ:
        _profileRepo.Verify(r => r.GetReaderByIdAsync(authorId, It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.GetChapterWithStoryAsync(chapterId, It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.AddAsync(It.IsAny<chapter_comment>(), It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.Verify(r => r.GetAsync(chapterId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _commentRepo.VerifyNoOtherCalls();

        _profileRepo.VerifyAll();
    }
}

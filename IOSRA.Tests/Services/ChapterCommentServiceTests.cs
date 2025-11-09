using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

using Service.Exceptions;
using Contract.DTOs.Respond.Common;          // PagedResult<T>
using Contract.DTOs.Respond.Chapter;        // ChapterCommentResponse, StoryCommentFeedResponse
using Repository.Entities;                  // chapter, story, chapter_comment, reader, account
using Repository.Interfaces;                // IChapterCommentRepository, IStoryCatalogRepository, IProfileRepository
using Service.Services;                     // ChapterCommentService

public class ChapterCommentServiceTests
{
    private readonly Mock<IChapterCommentRepository> _commentRepo;
    private readonly Mock<IStoryCatalogRepository> _storyRepo;
    private readonly Mock<IProfileRepository> _profileRepo;
    private readonly ChapterCommentService _svc;

    public ChapterCommentServiceTests()
    {
        // Strict: mọi call chưa setup sẽ lộ ra ngay
        _commentRepo = new Mock<IChapterCommentRepository>(MockBehavior.Strict);
        _storyRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
        _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);

        _svc = new ChapterCommentService(_commentRepo.Object, _storyRepo.Object, _profileRepo.Object);
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
                status = "published",     // service yêu cầu: chỉ cho cmt khi chapter published
                story = new story { story_id = storyId, status = "published" }
            }
        };

    // CASE: Happy path – lấy comment theo CHAPTER, có phân trang
    [Fact]
    public async Task GetByChapterAsync_Should_Return_Paged_Public_Comments()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        // chapter tồn tại + published + thuộc story published
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

        var res = await _svc.GetByChapterAsync(chapterId, page: 2, pageSize: 2, CancellationToken.None);

        // Kiểm tra khung phân trang + map field cơ bản
        res.Items.Should().HaveCount(2);
        res.Total.Should().Be(8);
        res.Page.Should().Be(2);
        res.PageSize.Should().Be(2);
        res.Items.Select(x => x.ChapterId).Distinct().Single().Should().Be(chapterId);
        res.Items.Select(x => x.Username).Should().BeEquivalentTo(new[] { "u1", "u2" });

        _commentRepo.VerifyAll();
        _storyRepo.VerifyNoOtherCalls();
        _profileRepo.VerifyNoOtherCalls();
    }

    // CASE: Lỗi – chapter không tồn tại
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
    }

    // CASE: Lỗi – chapter chưa publish (draft)
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
    }

    // CASE: Lỗi – story chưa publish
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
    }

    // CASE: Happy path – lấy FEED theo STORY, có filter theo chapterId
    [Fact]
    public async Task GetByStoryAsync_Should_Return_Feed_With_Mapped_Items_And_Filter()
    {
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        // story published
        _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new story { story_id = storyId, status = "published" });

        // filter chapter hợp lệ + thuộc story
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

        var res = await _svc.GetByStoryAsync(storyId, chapterId, page: 1, pageSize: 10, CancellationToken.None);

        // Feed có StoryId, filter id, và PagedResult<ChapterCommentResponse>
        res.StoryId.Should().Be(storyId);
        res.ChapterFilterId.Should().Be(chapterId);
        res.Comments.Items.Should().HaveCount(2);
        res.Comments.Total.Should().Be(2);
        res.Comments.Items.All(i => i.ChapterNo == 5 && i.ChapterTitle == "Ch5").Should().BeTrue();

        _commentRepo.VerifyAll();
        _storyRepo.VerifyAll();
        _profileRepo.VerifyNoOtherCalls();
    }

    // CASE: Lỗi – filter chapter không tồn tại
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
    }

    // CASE: Lỗi – filter chapter không thuộc story đang xem
    [Fact]
    public async Task GetByStoryAsync_Should_Throw_When_Chapter_Not_In_Story()
    {
        var storyId = Guid.NewGuid();
        var otherStoryId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new story { story_id = storyId, status = "published" });

        // chapter thuộc story khác
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
    }

    // CASE: Happy path – lấy FEED theo STORY, KHÔNG filter chapter
    [Fact]
    public async Task GetByStoryAsync_Should_Work_Without_Chapter_Filter()
    {
        var storyId = Guid.NewGuid();

        _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new story { story_id = storyId, status = "published" });

        var c1 = MakeComment(storyId, Guid.NewGuid(), 2, "Ch2", Guid.NewGuid(), "uA", "cmt");
        _commentRepo.Setup(r => r.GetByStoryAsync(storyId, null, 3, 5, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((new List<chapter_comment> { c1 }, 1));

        var res = await _svc.GetByStoryAsync(storyId, chapterId: null, page: 3, pageSize: 5, CancellationToken.None);

        res.StoryId.Should().Be(storyId);
        res.ChapterFilterId.Should().BeNull();
        res.Comments.Items.Should().HaveCount(1);
        res.Comments.Page.Should().Be(3);
        res.Comments.PageSize.Should().Be(5);

        _storyRepo.VerifyAll();
        _commentRepo.VerifyAll();
    }
}

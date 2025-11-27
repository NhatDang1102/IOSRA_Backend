using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Xunit;

public class ChapterCatalogServiceTests
{
    private readonly Mock<IChapterCatalogRepository> _chapterRepo;
    private readonly Mock<IStoryCatalogRepository> _storyRepo;
    private readonly Mock<IChapterPurchaseRepository> _purchaseRepo;
    private readonly ChapterCatalogService _svc;

    public ChapterCatalogServiceTests()
    {
        _chapterRepo = new Mock<IChapterCatalogRepository>(MockBehavior.Strict);
        _storyRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
        _purchaseRepo = new Mock<IChapterPurchaseRepository>(MockBehavior.Strict);
        _svc = new ChapterCatalogService(_chapterRepo.Object, _storyRepo.Object, _purchaseRepo.Object);
    }

    #region GetChaptersAsync

    // CASE: Page / PageSize không hợp lệ -> 400
    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    public async Task GetChaptersAsync_Should_Throw_When_Page_Or_PageSize_Invalid(int page, int pageSize)
    {
        var q = new ChapterCatalogQuery
        {
            StoryId = Guid.NewGuid(),
            Page = page,
            PageSize = pageSize
        };

        var act = () => _svc.GetChaptersAsync(q, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Page and PageSize must be positive integers*");

        _storyRepo.VerifyNoOtherCalls();
        _chapterRepo.VerifyNoOtherCalls();
    }

    // CASE: Story không tồn tại -> 404 StoryNotFound
    [Fact]
    public async Task GetChaptersAsync_Should_Throw_When_Story_Not_Found()
    {
        var storyId = Guid.NewGuid();
        var q = new ChapterCatalogQuery
        {
            StoryId = storyId,
            Page = 1,
            PageSize = 20
        };

        _storyRepo
            .Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((story?)null);

        var act = () => _svc.GetChaptersAsync(q, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("StoryNotFound");
        ex.Which.StatusCode.Should().Be(404);

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyNoOtherCalls();
    }

    // CASE: Happy path – map đúng list chapter + paging + IsLocked + LanguageCode
    [Fact]
    public async Task GetChaptersAsync_Should_Return_Paged_List_And_Map_Fields()
    {
        var storyId = Guid.NewGuid();
        var q = new ChapterCatalogQuery
        {
            StoryId = storyId,
            Page = 2,
            PageSize = 3
        };

        // Story tồn tại
        _storyRepo
            .Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new story
            {
                story_id = storyId,
                title = "Story X"
            });

        var ch1Id = Guid.NewGuid();
        var ch2Id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var ch1 = new chapter
        {
            chapter_id = ch1Id,
            story_id = storyId,
            chapter_no = 1,
            title = "Chap 1",
            word_count = 1000,
            access_type = "free",
            published_at = now,
            language = new language_list
            {
                lang_id = Guid.NewGuid(),
                lang_code = "vi",
                lang_name = "Vietnamese"
            }
        };

        var ch2 = new chapter
        {
            chapter_id = ch2Id,
            story_id = storyId,
            chapter_no = 2,
            title = "Chap 2",
            word_count = 2000,
            access_type = "Premium", // test case-insensitive IsLocked
            published_at = now.AddMinutes(5),
            // language = null -> LanguageCode = ""
        };

        _chapterRepo
            .Setup(r => r.GetPublishedChaptersByStoryAsync(
                storyId,
                q.Page,
                q.PageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<chapter> { ch1, ch2 }, 10));

        var res = await _svc.GetChaptersAsync(q, CancellationToken.None);

        res.Page.Should().Be(2);
        res.PageSize.Should().Be(3);
        res.Total.Should().Be(10);
        res.Items.Should().HaveCount(2);

        var item1 = res.Items[0];
        item1.ChapterId.Should().Be(ch1Id);
        item1.ChapterNo.Should().Be(1);
        item1.Title.Should().Be("Chap 1");
        item1.LanguageCode.Should().Be("vi");
        item1.WordCount.Should().Be(1000);
        item1.AccessType.Should().Be("free");
        item1.IsLocked.Should().BeFalse();
        item1.PublishedAt.Should().Be(ch1.published_at);

        var item2 = res.Items[1];
        item2.ChapterId.Should().Be(ch2Id);
        item2.ChapterNo.Should().Be(2);
        item2.Title.Should().Be("Chap 2");
        item2.LanguageCode.Should().BeEmpty();          // language null -> ""
        item2.WordCount.Should().Be(2000);
        item2.AccessType.Should().Be("Premium");
        item2.IsLocked.Should().BeTrue();               // vì không phải "free"
        item2.PublishedAt.Should().Be(ch2.published_at);

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    #endregion

    #region GetChapterAsync

    // CASE: Chapter không tồn tại -> 404 ChapterNotFound
    [Fact]
    public async Task GetChapterAsync_Should_Throw_When_Chapter_Not_Found()
    {
        var chapterId = Guid.NewGuid();

        _chapterRepo
            .Setup(r => r.GetPublishedChapterByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((chapter?)null);

        var act = () => _svc.GetChapterAsync(chapterId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("ChapterNotFound");
        ex.Which.StatusCode.Should().Be(404);

        _chapterRepo.VerifyAll();
        _storyRepo.VerifyNoOtherCalls();
    }

    // CASE: Chapter bị khóa (access_type != free) -> 403 ChapterLocked
    [Fact]
    public async Task GetChapterAsync_Should_Throw_When_Chapter_Is_Locked()
    {
        var chapterId = Guid.NewGuid();

        var ch = new chapter
        {
            chapter_id = chapterId,
            story_id = Guid.NewGuid(),
            chapter_no = 5,
            title = "Paid Chapter",
            access_type = "premium",
            content_url = "https://cdn/content",
            word_count = 1500,
            published_at = DateTime.UtcNow
        };

        _chapterRepo
            .Setup(r => r.GetPublishedChapterByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ch);

        var act = () => _svc.GetChapterAsync(chapterId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("ChapterLocked");
        ex.Which.StatusCode.Should().Be(403);
        ex.Which.Message.Should().Contain("requires purchase");

        _chapterRepo.VerifyAll();
        _storyRepo.VerifyNoOtherCalls();
    }

    // CASE: Chapter free nhưng không có content_url -> 500 ChapterContentMissing
    [Fact]
    public async Task GetChapterAsync_Should_Throw_When_Content_Missing()
    {
        var chapterId = Guid.NewGuid();

        var ch = new chapter
        {
            chapter_id = chapterId,
            story_id = Guid.NewGuid(),
            chapter_no = 3,
            title = "Broken Chapter",
            access_type = "free",
            content_url = "", // hoặc null
            word_count = 800,
            published_at = DateTime.UtcNow,
            language = new language_list
            {
                lang_id = Guid.NewGuid(),
                lang_code = "en",
                lang_name = "English"
            }
        };

        _chapterRepo
            .Setup(r => r.GetPublishedChapterByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ch);

        var act = () => _svc.GetChapterAsync(chapterId, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.ErrorCode.Should().Be("ChapterContentMissing");
        ex.Which.StatusCode.Should().Be(500);

        _chapterRepo.VerifyAll();
        _storyRepo.VerifyNoOtherCalls();
    }

    // CASE: Happy path – Chapter free + có content_url -> trả về detail đúng
    [Fact]
    public async Task GetChapterAsync_Should_Return_Detail_When_Free_And_Content_Available()
    {
        var chapterId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var ch = new chapter
        {
            chapter_id = chapterId,
            story_id = storyId,
            chapter_no = 10,
            title = "Free Chapter",
            access_type = "free",
            content_url = "https://cdn/chapters/free-10.html",
            word_count = 2500,
            published_at = now,
            language = new language_list
            {
                lang_id = Guid.NewGuid(),
                lang_code = "jp",
                lang_name = "Japanese"
            }
        };

        _chapterRepo
            .Setup(r => r.GetPublishedChapterByIdAsync(chapterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ch);

        var res = await _svc.GetChapterAsync(chapterId, CancellationToken.None);

        res.Should().NotBeNull();
        res.ChapterId.Should().Be(chapterId);
        res.StoryId.Should().Be(storyId);
        res.ChapterNo.Should().Be(10);
        res.Title.Should().Be("Free Chapter");
        res.LanguageCode.Should().Be("jp");
        res.WordCount.Should().Be(2500);
        res.AccessType.Should().Be("free");
        res.IsLocked.Should().BeFalse();
        res.PublishedAt.Should().Be(now);
        res.ContentUrl.Should().Be("https://cdn/chapters/free-10.html");

        _chapterRepo.VerifyAll();
        _storyRepo.VerifyNoOtherCalls();
    }

    #endregion
}

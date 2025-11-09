using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

using Service.Exceptions;
using Contract.DTOs.Request.Story;            // StoryCatalogQuery
using Contract.DTOs.Respond.Common;          // PagedResult<T>
using Contract.DTOs.Respond.Story;           // StoryCatalogListItemResponse
using Repository.Entities;                   // story, author, account, tag, story_tag, ...
using Repository.Interfaces;                 // IStoryCatalogRepository, IChapterCatalogRepository
using Service.Services;                      // StoryCatalogService

public class StoryCatalogServiceTests
{
    private readonly Mock<IStoryCatalogRepository> _storyRepo;
    private readonly Mock<IChapterCatalogRepository> _chapterRepo;
    private readonly StoryCatalogService _svc;

    public StoryCatalogServiceTests()
    {
        // Strict để lộ ra mọi call chưa được setup (dễ bắt sai khác hành vi)
        _storyRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
        _chapterRepo = new Mock<IChapterCatalogRepository>(MockBehavior.Strict);
        _svc = new StoryCatalogService(_storyRepo.Object, _chapterRepo.Object);
    }

    [Fact]
    public async Task GetStoriesAsync_Should_Return_Paged_List_And_Map_Basic_Fields()
    {
        // Arrange 
        // Query có page/size + bộ lọc nhẹ (query, tag)
        var q = new StoryCatalogQuery { Page = 2, PageSize = 3, Query = "abc", TagId = Guid.NewGuid() };

        // 2 stories ở trạng thái published, có author.account để mapper lấy username
        var s1 = new story
        {
            story_id = Guid.NewGuid(),
            title = "S1",
            status = "published",
            author_id = Guid.NewGuid(),
            author = new author
            {
                account_id = Guid.NewGuid(),
                account = new account
                {
                    account_id = Guid.NewGuid(),
                    username = "u1",
                    avatar_url = "a1.png"
                }
            },
            cover_url = "cover1.png",
            is_premium = false,
            desc = "desc1",
            published_at = DateTime.UtcNow
        };
        var s2 = new story
        {
            story_id = Guid.NewGuid(),
            title = "S2",
            status = "published",
            author_id = Guid.NewGuid(),
            author = new author
            {
                account_id = Guid.NewGuid(),
                account = new account
                {
                    account_id = Guid.NewGuid(),
                    username = "u2",
                    avatar_url = "a2.png"
                }
            },
            cover_url = "cover2.png",
            is_premium = false,
            desc = "desc2",
            published_at = DateTime.UtcNow
        };

        // Repo trả danh sách story + tổng bản ghi (phân trang)
        _storyRepo
            .Setup(r => r.SearchPublishedStoriesAsync(q.Query, q.TagId, q.AuthorId, q.Page, q.PageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<story> { s1, s2 }, 10)); // total=10

        // Service lấy thêm tổng chương cho từng story
        _chapterRepo
            .Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.Contains(s1.story_id) && ids.Contains(s2.story_id)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> {
                { s1.story_id, 12 }, { s2.story_id, 7 }
            });

        // Act 
        var res = await _svc.GetStoriesAsync(q, CancellationToken.None);

        // Assert 
        res.Page.Should().Be(2);
        res.PageSize.Should().Be(3);
        res.Total.Should().Be(10);
        res.Items.Should().HaveCount(2);

        res.Items.Select(i => i.StoryId).Should().BeEquivalentTo(new[] { s1.story_id, s2.story_id });
        res.Items.First(i => i.StoryId == s1.story_id).AuthorUsername.Should().Be("u1");
        res.Items.First(i => i.StoryId == s2.story_id).AuthorUsername.Should().Be("u2");

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    [Fact]
    public async Task GetStoriesAsync_Should_Return_Empty_When_No_Data()
    {
        // Arrange
        var q = new StoryCatalogQuery { Page = 1, PageSize = 5 };

        _storyRepo
            .Setup(r => r.SearchPublishedStoriesAsync(null, null, null, 1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<story>(), 0));

        // Act
        var res = await _svc.GetStoriesAsync(q, CancellationToken.None);

        // Assert 
        res.Items.Should().BeEmpty();
        res.Total.Should().Be(0);
        res.Page.Should().Be(1);
        res.PageSize.Should().Be(5);

        _storyRepo.VerifyAll();
        // Không gọi đếm chương khi không có story
        _chapterRepo.Verify(r => r.GetPublishedChapterCountsByStoryIdsAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetStoryAsync_Should_Map_AuthorUsername_Tags_And_TotalChapters()
    {
        // Arrange 
        var storyId = Guid.NewGuid();

        // author.account_id == story.author_id (1-1 với account)
        var accId = Guid.NewGuid();
        var author = new author
        {
            account_id = accId,
            account = new account { account_id = accId, username = "u_author", avatar_url = "a.png" }
        };

        // tags gắn qua story_tags (để mapper có TagId/TagName)
        var tagA = new tag { tag_id = Guid.NewGuid(), tag_name = "Fantasy" };
        var tagB = new tag { tag_id = Guid.NewGuid(), tag_name = "Adventure" };

        var s = new story
        {
            story_id = storyId,
            title = "Story X",
            status = "published",
            author_id = accId,
            author = author,
            cover_url = "cover.png",
            desc = "desc",
            is_premium = false,
            published_at = DateTime.UtcNow,
            story_tags = new List<story_tag>
            {
                new story_tag { story_id = storyId, tag_id = tagA.tag_id, tag = tagA },
                new story_tag { story_id = storyId, tag_id = tagB.tag_id, tag = tagB },
            }
        };

        _storyRepo
            .Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(s);

        // Service dùng hàm đơn để đếm chương của 1 story
        _chapterRepo
            .Setup(r => r.GetPublishedChapterCountAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);

        // Act 
        var res = await _svc.GetStoryAsync(storyId, CancellationToken.None);

        // Assert 
        res.StoryId.Should().Be(storyId);
        res.Title.Should().Be("Story X");
        res.AuthorUsername.Should().Be("u_author");
        res.TotalChapters.Should().Be(12);

        // Tags là List<StoryTagResponse> → kiểm Name/Id
        res.Tags.Should().HaveCount(2);
        res.Tags.Select(t => t.TagName)
                .Should().BeEquivalentTo(new[] { "Fantasy", "Adventure" }, o => o.WithoutStrictOrdering());
        res.Tags.Select(t => t.TagId)
                .Should().BeEquivalentTo(new[] { tagA.tag_id, tagB.tag_id }, o => o.WithoutStrictOrdering());

        _storyRepo.VerifyAll();
        _chapterRepo.VerifyAll();
    }

    [Fact]
    public async Task GetStoryAsync_Should_Throw_When_NotFound()
    {
        // Arrange 
        var storyId = Guid.NewGuid();

        _storyRepo
            .Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((story?)null);

        // Act & Assert 
        var act = () => _svc.GetStoryAsync(storyId, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>()
                 .WithMessage("Story was not found or not available.");

        _storyRepo.VerifyAll();
        _chapterRepo.Verify(
            r => r.GetPublishedChapterCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

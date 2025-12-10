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
using Repository.Entities;                   // story, author, account, tag, story_tag
using Repository.Interfaces;                 // IStoryCatalogRepository, IChapterCatalogRepository
using Service.Services;                      // StoryCatalogService

namespace IOSRA.Tests.Controllers
{
    public class StoryCatalogServiceTests
    {
        private readonly Mock<IStoryCatalogRepository> _storyRepo;
        private readonly Mock<IChapterCatalogRepository> _chapterRepo;
        private readonly StoryCatalogService _svc;

        public StoryCatalogServiceTests()
        {
            _storyRepo = new Mock<IStoryCatalogRepository>(MockBehavior.Strict);
            _chapterRepo = new Mock<IChapterCatalogRepository>(MockBehavior.Strict);
            _svc = new StoryCatalogService(_storyRepo.Object, _chapterRepo.Object);
        }

        // CASE: Danh sách – trả trang dữ liệu + map author
        [Fact]
        public async Task GetStoriesAsync_Should_Return_Paged_List_And_Map_Basic_Fields()
        {
            var q = new StoryCatalogQuery { Page = 2, PageSize = 3, Query = "abc", TagId = Guid.NewGuid() };

            var s1 = BuildStory("S1", "u1");
            var s2 = BuildStory("S2", "u2");

            _storyRepo.Setup(r => r.SearchPublishedStoriesAsync(q.Query, q.TagId, q.AuthorId, q.Page, q.PageSize, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((new List<story> { s1, s2 }, 10));

            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                        It.Is<IEnumerable<Guid>>(ids => ids.Contains(s1.story_id) && ids.Contains(s2.story_id)), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new Dictionary<Guid, int> { { s1.story_id, 12 }, { s2.story_id, 7 } });

            var res = await _svc.GetStoriesAsync(q, CancellationToken.None);

            res.Page.Should().Be(2);
            res.PageSize.Should().Be(3);
            res.Total.Should().Be(10);
            res.Items.Should().HaveCount(2);
            res.Items.First(i => i.StoryId == s1.story_id).AuthorUsername.Should().Be("u1");
            res.Items.First(i => i.StoryId == s2.story_id).AuthorUsername.Should().Be("u2");

            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE: Danh sách rỗng → không gọi đếm chương
        [Fact]
        public async Task GetStoriesAsync_Should_Return_Empty_When_No_Data()
        {
            var q = new StoryCatalogQuery { Page = 1, PageSize = 5 };

            _storyRepo.Setup(r => r.SearchPublishedStoriesAsync(null, null, null, 1, 5, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((new List<story>(), 0));

            var res = await _svc.GetStoriesAsync(q, CancellationToken.None);

            res.Items.Should().BeEmpty();
            res.Total.Should().Be(0);

            _chapterRepo.Verify(r => r.GetPublishedChapterCountsByStoryIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
            _storyRepo.VerifyAll();
        }

        // CASE (thêm): Repo đếm chương trả thiếu id → service mặc định 0
        [Fact]
        public async Task GetStoriesAsync_Should_Default_Missing_ChapterCount_To_Zero()
        {
            var q = new StoryCatalogQuery { Page = 1, PageSize = 10 };
            var s1 = BuildStory("A", "aa");
            var s2 = BuildStory("B", "bb");

            _storyRepo.Setup(r => r.SearchPublishedStoriesAsync(null, null, null, 1, 10, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((new List<story> { s1, s2 }, 2));

            // chỉ trả count cho s1, thiếu s2
            _chapterRepo.Setup(r => r.GetPublishedChapterCountsByStoryIdsAsync(
                        It.Is<IEnumerable<Guid>>(ids => ids.Contains(s1.story_id) && ids.Contains(s2.story_id)), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(new Dictionary<Guid, int> { { s1.story_id, 5 } });

            var res = await _svc.GetStoriesAsync(q, CancellationToken.None);

            res.Items.First(i => i.StoryId == s1.story_id).TotalChapters.Should().Be(5);
            res.Items.First(i => i.StoryId == s2.story_id).TotalChapters.Should().Be(0); // mặc định 0
        }

        // CASE: Chi tiết – map AuthorUsername, Tags, TotalChapters
        [Fact]
        public async Task GetStoryAsync_Should_Map_AuthorUsername_Tags_And_TotalChapters()
        {
            var storyId = Guid.NewGuid();

            var accId = Guid.NewGuid();
            var author = new author
            {
                account_id = accId,
                account = new account { account_id = accId, username = "u_author", avatar_url = "a.png" }
            };

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

            _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(s);

            _chapterRepo.Setup(r => r.GetPublishedChapterCountAsync(storyId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(12);

            var res = await _svc.GetStoryAsync(storyId, CancellationToken.None);

            res.AuthorUsername.Should().Be("u_author");
            res.TotalChapters.Should().Be(12);
            res.Tags.Select(t => t.TagName).Should().BeEquivalentTo(new[] { "Fantasy", "Adventure" }, o => o.WithoutStrictOrdering());

            _storyRepo.VerifyAll();
            _chapterRepo.VerifyAll();
        }

        // CASE (thêm): Chi tiết – story có 0 tag → Tags rỗng
        [Fact]
        public async Task GetStoryAsync_Should_Handle_Empty_Tags()
        {
            var storyId = Guid.NewGuid();
            var accId = Guid.NewGuid();

            var s = new story
            {
                story_id = storyId,
                title = "NoTag",
                status = "published",
                author_id = accId,
                author = new author { account_id = accId, account = new account { account_id = accId, username = "u0" } },
                story_tags = new List<story_tag>() // rỗng
            };

            _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(s);
            _chapterRepo.Setup(r => r.GetPublishedChapterCountAsync(storyId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(0);

            var res = await _svc.GetStoryAsync(storyId, CancellationToken.None);

            res.Tags.Should().BeEmpty();
            res.TotalChapters.Should().Be(0);
        }

        // CASE: Not found → throw AppException
        [Fact]
        public async Task GetStoryAsync_Should_Throw_When_NotFound()
        {
            var storyId = Guid.NewGuid();

            _storyRepo.Setup(r => r.GetPublishedStoryByIdAsync(storyId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((story?)null);

            var act = () => _svc.GetStoryAsync(storyId, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("Story was not found or not available.");

            _chapterRepo.Verify(r => r.GetPublishedChapterCountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            _storyRepo.VerifyAll();
        }

        // --- helpers ---
        private static story BuildStory(string title, string authorUsername)
        {
            var accId = Guid.NewGuid();
            return new story
            {
                story_id = Guid.NewGuid(),
                title = title,
                status = "published",
                author_id = accId,
                author = new author
                {
                    account_id = accId,
                    account = new account { account_id = accId, username = authorUsername, avatar_url = "x.png" }
                },
                cover_url = "cover.png",
                is_premium = false,
                desc = "desc",
                published_at = DateTime.UtcNow
            };
        }
    }
}

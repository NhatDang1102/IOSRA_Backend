using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Story;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class StoryCatalogControllerTests
    {
        private readonly Mock<IStoryCatalogService> _storyCatalogService;
        private readonly Mock<IStoryHighlightService> _storyHighlightService;
        private readonly StoryCatalogController _controller;

        public StoryCatalogControllerTests()
        {
            _storyCatalogService = new Mock<IStoryCatalogService>(MockBehavior.Strict);
            _storyHighlightService = new Mock<IStoryHighlightService>(MockBehavior.Strict);
            _controller = new StoryCatalogController(
                _storyCatalogService.Object,
                _storyHighlightService.Object);
        }

        [Fact]
        public async Task List_Should_Map_Query_Params_And_Return_PagedResult()
        {
            // Arrange
            int page = 2;
            int pageSize = 30;
            string? query = "magic";
            Guid? tagId = Guid.NewGuid();
            Guid? authorId = Guid.NewGuid();
            string? languageCode = "en-US";

            var expected = new PagedResult<StoryCatalogListItemResponse>
            {
                Page = page,
                PageSize = pageSize,
                Total = 1,
                Items = new List<StoryCatalogListItemResponse>
        {
            new StoryCatalogListItemResponse
            {
                StoryId = Guid.NewGuid(),
                Title = "Story 1",
                AuthorId = authorId.Value,
                AuthorUsername = "author1",
                LengthPlan = "short"
            }
        }
            };

            _storyCatalogService
                .Setup(s => s.GetStoriesAsync(
                    It.Is<StoryCatalogQuery>(q =>
                        q.Page == page &&
                        q.PageSize == pageSize &&
                        q.Query == query &&
                        q.TagId == tagId &&
                        q.AuthorId == authorId &&
                        q.LanguageCode == languageCode),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(page, pageSize, query, tagId, authorId, languageCode, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyCatalogService.VerifyAll();
            _storyHighlightService.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task Filter_Should_Call_Advanced_Service_And_Return_PagedResult()
        {
            // Arrange
            int page = 1;
            int pageSize = 20;
            string? queryText = "romance";
            Guid? tagId = Guid.NewGuid();
            Guid? authorId = Guid.NewGuid();
            string? languageCode = "vi-VN";
            bool? isPremium = true;
            double? minAvgRating = 4.5;
            string? sortBy = "TopRated";
            string? sortDir = "Desc";

            var expected = new PagedResult<StoryCatalogListItemResponse>
            {
                Page = page,
                PageSize = pageSize,
                Total = 0,
                Items = new List<StoryCatalogListItemResponse>()
            };

            _storyCatalogService
                .Setup(s => s.GetStoriesAdvancedAsync(
                    It.Is<StoryCatalogQuery>(q =>
                        q.Page == page &&
                        q.PageSize == pageSize &&
                        q.Query == queryText &&
                        q.TagId == tagId &&
                        q.AuthorId == authorId &&
                        q.LanguageCode == languageCode &&
                        q.IsPremium == isPremium &&
                        q.MinAvgRating == minAvgRating &&
                        q.SortBy == StorySortBy.TopRated &&
                        q.SortDir == SortDir.Desc
                    ),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Filter(
                page,
                pageSize,
                queryText,
                tagId,
                authorId,
                languageCode,
                isPremium,
                minAvgRating,
                sortBy,
                sortDir,
                CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyCatalogService.VerifyAll();
            _storyHighlightService.VerifyNoOtherCalls();
        }


        [Fact]
        public async Task Latest_Should_Call_HighlightService_And_Return_List()
        {
            // Arrange
            int limit = 5;
            var expected = (IReadOnlyList<StoryCatalogListItemResponse>)new List<StoryCatalogListItemResponse>
                {
                    new StoryCatalogListItemResponse
                    {
                        StoryId = Guid.NewGuid(),
                        Title = "Latest story",
                        AuthorId = Guid.NewGuid(),
                        AuthorUsername = "author",
                        LengthPlan = "long"
                    }
                };

            _storyHighlightService
                .Setup(h => h.GetLatestStoriesAsync(limit, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Latest(limit, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyHighlightService.VerifyAll();
            _storyCatalogService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task TopWeekly_Should_Call_HighlightService_And_Return_List()
        {
            // Arrange
            int limit = 10;
            var expected = (IReadOnlyList<StoryWeeklyHighlightResponse>)new List<StoryWeeklyHighlightResponse>
                {
                    new StoryWeeklyHighlightResponse
                    {
                        Story = new StoryCatalogListItemResponse
                        {
                            StoryId = Guid.NewGuid(),
                            Title = "Weekly top",
                            AuthorId = Guid.NewGuid(),
                            AuthorUsername = "author",
                            LengthPlan = "medium"
                        },
                        WeeklyViewCount = 1000,
                        WeekStartUtc = DateTime.UtcNow.Date
                    }
                };

            _storyHighlightService
                .Setup(h => h.GetTopWeeklyStoriesAsync(limit, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.TopWeekly(limit, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyHighlightService.VerifyAll();
            _storyCatalogService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Get_Should_Call_Service_With_StoryId_And_Return_Detail()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var expected = new StoryCatalogDetailResponse
            {
                StoryId = storyId,
                Title = "Detail story",
                AuthorId = Guid.NewGuid(),
                AuthorUsername = "author",
                LengthPlan = "short"
            };

            _storyCatalogService
                .Setup(s => s.GetStoryAsync(storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Get(storyId, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyCatalogService.VerifyAll();
            _storyHighlightService.VerifyNoOtherCalls();
        }
    }
}

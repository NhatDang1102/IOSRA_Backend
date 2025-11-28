using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;   // ChapterModerationQueueItem
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class ChapterModerationControllerTests
    {
        private readonly Mock<IChapterModerationService> _chapterModerationService;
        private readonly ChapterModerationController _controller;
        private readonly Guid _accountId = Guid.NewGuid();

        public ChapterModerationControllerTests()
        {
            _chapterModerationService = new Mock<IChapterModerationService>(MockBehavior.Strict);
            _controller = new ChapterModerationController(_chapterModerationService.Object);

            SetUserWithAccountId(_accountId);
        }

        private void SetUserWithAccountId(Guid accountId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString())
            }, "TestAuth");

            var user = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            };
        }

        [Fact]
        public async Task List_Should_Call_Service_With_Status_And_Return_Ok()
        {
            // Arrange
            var status = "pending";

            var expected = (IReadOnlyList<ChapterModerationQueueItem>)new List<ChapterModerationQueueItem>
            {
                new ChapterModerationQueueItem
                {
                    ReviewId = Guid.NewGuid(),
                    ChapterId = Guid.NewGuid(),
                    StoryId = Guid.NewGuid(),
                    StoryTitle = "Story 1",
                    ChapterTitle = "Chapter 1",
                    AuthorId = Guid.NewGuid(),
                    AuthorUsername = "author1",
                    AuthorEmail = "author1@example.com",
                    ChapterNo = 1,
                    WordCount = 1200,
                    LanguageCode = "en",
                    LanguageName = "English",
                    PriceDias = 0,
                    Status = status,
                    SubmittedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            _chapterModerationService
                .Setup(s => s.ListAsync(status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _chapterModerationService.VerifyAll();
        }

        [Fact]
        public async Task List_Should_Call_Service_With_Null_Status_When_Not_Provided()
        {
            // Arrange
            string? status = null;

            var expected = (IReadOnlyList<ChapterModerationQueueItem>)new List<ChapterModerationQueueItem>();

            _chapterModerationService
                .Setup(s => s.ListAsync(status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _chapterModerationService.VerifyAll();
        }

        [Fact]
        public async Task Get_Should_Call_Service_With_ReviewId_And_Return_Ok()
        {
            // Arrange
            var reviewId = Guid.NewGuid();

            var expected = new ChapterModerationQueueItem
            {
                ReviewId = reviewId,
                ChapterId = Guid.NewGuid(),
                StoryId = Guid.NewGuid(),
                StoryTitle = "Story 1",
                ChapterTitle = "Chapter 1",
                AuthorId = Guid.NewGuid(),
                AuthorUsername = "author1",
                AuthorEmail = "author1@example.com",
                ChapterNo = 1,
                WordCount = 1200,
                LanguageCode = "en",
                LanguageName = "English",
                PriceDias = 0,
                Status = "pending",
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _chapterModerationService
                .Setup(s => s.GetAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Get(reviewId, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _chapterModerationService.VerifyAll();
        }

        [Fact]
        public async Task Decide_Should_Call_ModerateAsync_And_Return_Approved_Message_When_Approve_True()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var req = new ChapterModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = "OK"
            };

            _chapterModerationService
                .Setup(s => s.ModerateAsync(_accountId, reviewId, req, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _controller.Decide(reviewId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Chapter approved." });

            _chapterModerationService.VerifyAll();
        }

        [Fact]
        public async Task Decide_Should_Call_ModerateAsync_And_Return_Rejected_Message_When_Approve_False()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var req = new ChapterModerationDecisionRequest
            {
                Approve = false,
                ModeratorNote = "Not appropriate"
            };

            _chapterModerationService
                .Setup(s => s.ModerateAsync(_accountId, reviewId, req, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _controller.Decide(reviewId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Chapter rejected." });

            _chapterModerationService.VerifyAll();
        }
    }
}

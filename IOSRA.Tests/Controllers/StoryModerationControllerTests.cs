using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Story;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class StoryModerationControllerTests
    {
        private readonly Mock<IStoryModerationService> _storyModerationService;
        private readonly StoryModerationController _controller;
        private readonly Guid _accountId = Guid.NewGuid();

        public StoryModerationControllerTests()
        {
            _storyModerationService = new Mock<IStoryModerationService>(MockBehavior.Strict);
            _controller = new StoryModerationController(_storyModerationService.Object);

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

            var expected = (IReadOnlyList<StoryModerationQueueItem>)new List<StoryModerationQueueItem>
            {
                new StoryModerationQueueItem
                {
                    ReviewId = Guid.NewGuid(),
                    StoryId = Guid.NewGuid(),
                    AuthorId = Guid.NewGuid(),
                    Title = "Story 1",
                    AuthorUsername = "author1",
                    Status = status,
                    Outline = "outline",
                    LengthPlan = "short",
                    SubmittedAt = DateTime.UtcNow
                }
            };

            _storyModerationService
                .Setup(s => s.ListAsync(status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyModerationService.VerifyAll();
        }

        [Fact]
        public async Task List_Should_Call_Service_With_Null_Status_When_Not_Provided()
        {
            // Arrange
            string? status = null;

            var expected = (IReadOnlyList<StoryModerationQueueItem>)new List<StoryModerationQueueItem>();

            _storyModerationService
                .Setup(s => s.ListAsync(status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyModerationService.VerifyAll();
        }

        [Fact]
        public async Task Get_Should_Call_Service_With_ReviewId_And_Return_Ok()
        {
            // Arrange
            var reviewId = Guid.NewGuid();

            var expected = new StoryModerationQueueItem
            {
                ReviewId = reviewId,
                StoryId = Guid.NewGuid(),
                AuthorId = Guid.NewGuid(),
                Title = "Story 1",
                AuthorUsername = "author1",
                Status = "pending",
                Outline = "outline",
                LengthPlan = "short",
                SubmittedAt = DateTime.UtcNow
            };

            _storyModerationService
                .Setup(s => s.GetAsync(reviewId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Get(reviewId, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _storyModerationService.VerifyAll();
        }

        [Fact]
        public async Task Decide_Should_Call_ModerateAsync_And_Return_Approved_Message_When_Approve_True()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var req = new StoryModerationDecisionRequest
            {
                Approve = true,
                ModeratorNote = "Looks good"
            };

            _storyModerationService
                .Setup(s => s.ModerateAsync(_accountId, reviewId, req, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _controller.Decide(reviewId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Story approved." });

            _storyModerationService.VerifyAll();
        }

        [Fact]
        public async Task Decide_Should_Call_ModerateAsync_And_Return_Rejected_Message_When_Approve_False()
        {
            // Arrange
            var reviewId = Guid.NewGuid();
            var req = new StoryModerationDecisionRequest
            {
                Approve = false,
                ModeratorNote = "Not appropriate"
            };

            _storyModerationService
                .Setup(s => s.ModerateAsync(_accountId, reviewId, req, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _controller.Decide(reviewId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Story rejected." });

            _storyModerationService.VerifyAll();
        }
    }
}

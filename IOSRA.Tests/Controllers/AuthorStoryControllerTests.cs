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
    public class AuthorStoryControllerTests
    {
        private readonly Mock<IAuthorStoryService> _authorStoryService;
        private readonly AuthorStoryController _controller;
        private readonly Guid _accountId = Guid.NewGuid();

        public AuthorStoryControllerTests()
        {
            _authorStoryService = new Mock<IAuthorStoryService>(MockBehavior.Strict);
            _controller = new AuthorStoryController(_authorStoryService.Object);

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
        public async Task List_Should_Call_Service_With_AccountId_And_Status()
        {
            // Arrange
            var status = "draft";

            var expected = (IReadOnlyList<StoryListItemResponse>)new List<StoryListItemResponse>
            {
                new StoryListItemResponse
                {
                    StoryId = Guid.NewGuid(),
                    Title = "Story 1",
                    Status = status,
                    IsPremium = false,
                    LengthPlan = "short",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _authorStoryService
                .Setup(s => s.ListAsync(_accountId, status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorStoryService.VerifyAll();
        }

        [Fact]
        public async Task List_Should_Call_Service_With_Null_Status_When_Not_Provided()
        {
            // Arrange
            string? status = null;

            var expected = (IReadOnlyList<StoryListItemResponse>)new List<StoryListItemResponse>();

            _authorStoryService
                .Setup(s => s.ListAsync(_accountId, status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorStoryService.VerifyAll();
        }

        [Fact]
        public async Task Get_Should_Call_Service_With_AccountId_And_StoryId()
        {
            // Arrange
            var storyId = Guid.NewGuid();

            var expected = new StoryResponse
            {
                StoryId = storyId,
                Title = "My story",
                Status = "draft",
                IsPremium = false,
                Outline = "outline",
                LengthPlan = "short",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _authorStoryService
                .Setup(s => s.GetAsync(_accountId, storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Get(storyId, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorStoryService.VerifyAll();
        }

        [Fact]
        public async Task Create_Should_Call_Service_With_AccountId_And_Request()
        {
            // Arrange
            var req = new StoryCreateRequest
            {
                Title = "New story",
                Description = "desc",
                TagIds = new List<Guid> { Guid.NewGuid() },
                CoverMode = "upload",
                Outline = "this is outline with enough length to pass validation",
                LengthPlan = "short",
                CoverPrompt = "prompt"
            };

            var expected = new StoryResponse
            {
                StoryId = Guid.NewGuid(),
                Title = req.Title,
                Status = "draft",
                IsPremium = false,
                Outline = req.Outline,
                LengthPlan = req.LengthPlan,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _authorStoryService
                .Setup(s => s.CreateAsync(_accountId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Create(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorStoryService.VerifyAll();
        }

        [Fact]
        public async Task Update_Should_Call_Service_With_AccountId_StoryId_And_Request()
        {
            // Arrange
            var storyId = Guid.NewGuid();

            var req = new StoryUpdateRequest
            {
                Title = "Updated title",
                Description = "Updated desc",
                Outline = "updated outline",
                LengthPlan = "novel",
                TagIds = new List<Guid> { Guid.NewGuid() },
                CoverMode = "upload",
                CoverPrompt = "new prompt"
            };

            var expected = new StoryResponse
            {
                StoryId = storyId,
                Title = req.Title!,
                Status = "draft",
                IsPremium = false,
                Outline = req.Outline!,
                LengthPlan = req.LengthPlan!,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };

            _authorStoryService
                .Setup(s => s.UpdateDraftAsync(_accountId, storyId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Update(storyId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorStoryService.VerifyAll();
        }

        [Fact]
        public async Task Submit_Should_Call_Service_With_AccountId_StoryId_And_Request()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var req = new StorySubmitRequest();

            var expected = new StoryResponse
            {
                StoryId = storyId,
                Title = "My story",
                Status = "pending_review",
                IsPremium = false,
                Outline = "outline",
                LengthPlan = "short",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };

            _authorStoryService
                .Setup(s => s.SubmitForReviewAsync(_accountId, storyId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Submit(storyId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorStoryService.VerifyAll();
        }

        [Fact]
        public async Task Complete_Should_Call_Service_With_AccountId_And_StoryId()
        {
            // Arrange
            var storyId = Guid.NewGuid();

            var expected = new StoryResponse
            {
                StoryId = storyId,
                Title = "My story",
                Status = "completed",
                IsPremium = true,
                Outline = "outline",
                LengthPlan = "short",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow
            };

            _authorStoryService
                .Setup(s => s.CompleteAsync(_accountId, storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Complete(storyId, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorStoryService.VerifyAll();
        }
    }
}

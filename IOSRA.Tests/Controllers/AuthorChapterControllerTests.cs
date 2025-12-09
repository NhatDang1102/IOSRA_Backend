using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class AuthorChapterControllerTests
    {
        private readonly Mock<IAuthorChapterService> _authorChapterService;
        private readonly AuthorChapterController _controller;
        private readonly Guid _accountId = Guid.NewGuid();

        public AuthorChapterControllerTests()
        {
            _authorChapterService = new Mock<IAuthorChapterService>(MockBehavior.Strict);
            _controller = new AuthorChapterController(_authorChapterService.Object);

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
        public async Task List_Should_Call_Service_With_AccountId_StoryId_And_Status()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var status = "draft";

            var expected = (IReadOnlyList<ChapterListItemResponse>)new List<ChapterListItemResponse>
            {
                new ChapterListItemResponse
                {
                    ChapterId = Guid.NewGuid(),
                    ChapterNo = 1,
                    Title = "Chapter 1",
                    WordCount = 1000,
                    CharCount = 5000,
                    LanguageCode = "en",
                    LanguageName = "English",
                    PriceDias = 0,
                    Status = status,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _authorChapterService
                .Setup(s => s.GetAllAsync(_accountId, storyId, status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.GetAll(storyId, status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorChapterService.VerifyAll();
        }

        [Fact]
        public async Task List_Should_Pass_Null_Status_When_Not_Provided()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            string? status = null;

            var expected = (IReadOnlyList<ChapterListItemResponse>)new List<ChapterListItemResponse>();

            _authorChapterService
                .Setup(s => s.GetAllAsync(_accountId, storyId, status, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.GetAll(storyId, status, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorChapterService.VerifyAll();
        }

        [Fact]
        public async Task Get_Should_Call_Service_With_AccountId_StoryId_And_ChapterId()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var chapterId = Guid.NewGuid();

            var expected = new ChapterResponse
            {
                ChapterId = chapterId,
                StoryId = storyId,
                ChapterNo = 1,
                Title = "Chapter 1",
                WordCount = 1000,
                CharCount = 5000,
                LanguageCode = "en",
                LanguageName = "English",
                Status = "draft",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };

            _authorChapterService
                .Setup(s => s.GetByIdAsync(_accountId, storyId, chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.GetById(storyId, chapterId, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorChapterService.VerifyAll();
        }

        [Fact]
        public async Task Create_Should_Call_Service_With_AccountId_StoryId_And_Request()
        {
            // Arrange
            var storyId = Guid.NewGuid();

            var req = new ChapterCreateRequest
            {
                Title = "New chapter",
                LanguageCode = "en",
                Content = "chapter content",
                AccessType = "free"
            };

            var expected = new ChapterResponse
            {
                ChapterId = Guid.NewGuid(),
                StoryId = storyId,
                ChapterNo = 1,
                Title = req.Title,
                LanguageCode = req.LanguageCode,
                LanguageName = "English",
                Status = "draft",
                WordCount = 1000,
                CharCount = 5000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _authorChapterService
                .Setup(s => s.CreateAsync(_accountId, storyId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.Create(storyId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorChapterService.VerifyAll();
        }

        [Fact]
        public async Task Update_Should_Call_Service_With_AccountId_StoryId_ChapterId_And_Request()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var chapterId = Guid.NewGuid();

            var req = new ChapterUpdateRequest
            {
                Title = "Updated title",
                LanguageCode = "vi",
                Content = "updated content",
                AccessType = "dias"
            };

            var expected = new ChapterResponse
            {
                ChapterId = chapterId,
                StoryId = storyId,
                ChapterNo = 1,
                Title = req.Title!,
                LanguageCode = req.LanguageCode!,
                LanguageName = "Vietnamese",
                Status = "draft",
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow
            };

            _authorChapterService
                .Setup(s => s.UpdateDraftAsync(_accountId, storyId, chapterId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.UpdateDraft(storyId, chapterId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorChapterService.VerifyAll();
        }

        [Fact]
        public async Task Submit_Should_Call_Service_With_AccountId_ChapterId_And_Request()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var req = new ChapterSubmitRequest();

            var expected = new ChapterResponse
            {
                ChapterId = chapterId,
                StoryId = Guid.NewGuid(),
                ChapterNo = 1,
                Title = "Chapter 1",
                LanguageCode = "en",
                LanguageName = "English",
                Status = "pending_review",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };

            _authorChapterService
                .Setup(s => s.SubmitAsync(_accountId, chapterId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.SubmitForReview(chapterId, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorChapterService.VerifyAll();
        }

        [Fact]
        public async Task Withdraw_Should_Call_Service_With_AccountId_And_ChapterId()
        {
            // Arrange
            var chapterId = Guid.NewGuid();

            var expected = new ChapterResponse
            {
                ChapterId = chapterId,
                StoryId = Guid.NewGuid(),
                ChapterNo = 1,
                Title = "Chapter 1",
                LanguageCode = "en",
                LanguageName = "English",
                Status = "draft",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow
            };

            _authorChapterService
                .Setup(s => s.WithdrawAsync(_accountId, chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.WithdrawChapter(chapterId, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _authorChapterService.VerifyAll();
        }
    }
}

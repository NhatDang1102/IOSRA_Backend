using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Exceptions;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class ChapterCatalogControllerTests
    {
        private readonly Mock<IChapterCatalogService> _chapterCatalogService;
        private readonly Mock<IStoryViewTracker> _storyViewTracker;
        private readonly ChapterCatalogController _controller;
        private readonly Guid _accountId = Guid.NewGuid();

        public ChapterCatalogControllerTests()
        {
            _chapterCatalogService = new Mock<IChapterCatalogService>(MockBehavior.Strict);
            _storyViewTracker = new Mock<IStoryViewTracker>(MockBehavior.Strict);

            _controller = new ChapterCatalogController(
                _chapterCatalogService.Object,
                _storyViewTracker.Object);

            SetUserWithAccountId(_accountId);
        }

        private void SetUserWithAccountId(Guid accountId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString())
            }, "TestAuth");

            var user = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext
            {
                User = user
            };

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task List_Should_Throw_AppException_When_StoryId_Is_Empty()
        {
            // Arrange
            var query = new ChapterCatalogQuery
            {
                StoryId = Guid.Empty,
                Page = 1,
                PageSize = 10
            };

            // Act
            var act = async () => await _controller.List(query, CancellationToken.None);

            // Assert
            var ex = await Assert.ThrowsAsync<AppException>(act);
            ex.ErrorCode.Should().Be("ValidationFailed");
            ex.Message.Should().Contain("storyId is required");

            _chapterCatalogService.VerifyNoOtherCalls();
            _storyViewTracker.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task List_Should_Call_Service_And_Return_PagedResult_When_StoryId_Valid()
        {
            // Arrange
            var query = new ChapterCatalogQuery
            {
                StoryId = Guid.NewGuid(),
                Page = 2,
                PageSize = 20
            };

            var expected = new PagedResult<ChapterCatalogListItemResponse>
            {
                Page = query.Page,
                PageSize = query.PageSize,
                Total = 1,
                Items = new List<ChapterCatalogListItemResponse>
                {
                    new ChapterCatalogListItemResponse
                    {
                        ChapterId = Guid.NewGuid(),
                        ChapterNo = 1,
                        Title = "Chapter 1",
                        LanguageCode = "en",
                        WordCount = 1000,
                        AccessType = "free",
                        IsLocked = false,
                        PriceDias = 0,
                        PublishedAt = DateTime.UtcNow
                    }
                }
            };

            _chapterCatalogService
                .Setup(s => s.GetChaptersAsync(query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.List(query, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _chapterCatalogService.VerifyAll();
            _storyViewTracker.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Get_Should_Call_Service_And_Record_View_Then_Return_Detail()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var chapter = new ChapterCatalogDetailResponse
            {
                ChapterId = chapterId,
                StoryId = Guid.NewGuid(),
                ChapterNo = 1,
                Title = "Chapter 1",
                LanguageCode = "en",
                WordCount = 1000,
                AccessType = "free",
                IsLocked = false,
                PriceDias = 0,
                PublishedAt = DateTime.UtcNow,
                ContentUrl = "https://example.com/content"
            };

            // Mock IP để tạo fingerprint
            var ip = IPAddress.Parse("127.0.0.1");
            _controller.HttpContext.Connection.RemoteIpAddress = ip;

            _chapterCatalogService
                .Setup(s => s.GetChapterAsync(chapterId, It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
                .ReturnsAsync(chapter)
                .Verifiable();

            _storyViewTracker
                .Setup(t => t.RecordViewAsync(
                    chapter.StoryId,
                    It.IsAny<Guid?>(),
                    It.Is<string?>(fp => fp == ip.ToString()),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            // Act
            var result = await _controller.Get(chapterId, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(chapter);

            _chapterCatalogService.VerifyAll();
            _storyViewTracker.VerifyAll();
        }

        [Fact]
        public async Task GetVoices_Should_Call_Service_And_Return_List()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var expected = (IReadOnlyList<ChapterCatalogVoiceResponse>)new List<ChapterCatalogVoiceResponse>
            {
                new ChapterCatalogVoiceResponse
                {
                    VoiceId = Guid.NewGuid(),
                    VoiceName = "Voice 1",
                    VoiceCode = "v1",
                    Status = "ready",
                    PriceDias = 10,
                    HasAudio = true,
                    Owned = false,
                    AudioUrl = "https://example.com/audio1"
                }
            };

            _chapterCatalogService
                .Setup(s => s.GetChapterVoicesAsync(chapterId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.GetVoices(chapterId, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _chapterCatalogService.VerifyAll();
            _storyViewTracker.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetVoice_Should_Call_Service_And_Return_Single_Voice()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var voiceId = Guid.NewGuid();

            var expected = new ChapterCatalogVoiceResponse
            {
                VoiceId = voiceId,
                VoiceName = "Voice 1",
                VoiceCode = "v1",
                Status = "ready",
                PriceDias = 20,
                HasAudio = true,
                Owned = true,
                AudioUrl = "https://example.com/audio"
            };

            _chapterCatalogService
                .Setup(s => s.GetChapterVoiceAsync(chapterId, voiceId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected)
                .Verifiable();

            // Act
            var result = await _controller.GetVoice(chapterId, voiceId, CancellationToken.None);

            // Assert
            var ok = result.Result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _chapterCatalogService.VerifyAll();
            _storyViewTracker.VerifyNoOtherCalls();
        }
    }
}

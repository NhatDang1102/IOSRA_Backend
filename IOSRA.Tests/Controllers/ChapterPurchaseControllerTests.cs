using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Voice;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class ChapterPurchaseControllerTests
    {
        private readonly Mock<IChapterPurchaseService> _serviceMock;
        private readonly ChapterPurchaseController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public ChapterPurchaseControllerTests()
        {
            _serviceMock = new Mock<IChapterPurchaseService>();
            _controller = new ChapterPurchaseController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task PurchaseChapter_Should_Return_Ok()
        {
            var chapterId = Guid.NewGuid();
            var res = new ChapterPurchaseResponse();
            _serviceMock.Setup(s => s.PurchaseAsync(_userId, chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Purchase(chapterId, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task PurchaseVoices_Should_Return_Ok()
        {
            var chapterId = Guid.NewGuid();
            var req = new ChapterVoicePurchaseRequest { VoiceIds = new List<Guid> { Guid.NewGuid() } };
            var res = new ChapterVoicePurchaseResponse();
            
            _serviceMock.Setup(s => s.PurchaseVoicesAsync(_userId, chapterId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.PurchaseVoices(chapterId, req, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task GetPurchasedChapters_Should_Return_Ok()
        {
            var res = new List<PurchasedChapterResponse>();
            _serviceMock.Setup(s => s.GetPurchasedChaptersAsync(_userId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetChapterHistory(null, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

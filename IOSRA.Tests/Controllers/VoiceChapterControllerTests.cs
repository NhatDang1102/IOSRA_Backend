using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Voice;
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
    public class VoiceChapterControllerTests
    {
        private readonly Mock<IVoiceChapterService> _voiceChapterServiceMock;
        private readonly Mock<IVoicePricingService> _voicePricingServiceMock;
        private readonly VoiceChapterController _controller;
        private readonly Guid _authorId = Guid.NewGuid();
        private readonly Guid _chapterId = Guid.NewGuid();

        public VoiceChapterControllerTests()
        {
            _voiceChapterServiceMock = new Mock<IVoiceChapterService>();
            _voicePricingServiceMock = new Mock<IVoicePricingService>();
            _controller = new VoiceChapterController(_voiceChapterServiceMock.Object, _voicePricingServiceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_authorId);
        }

        [Fact]
        public async Task GetStatus_Should_Return_Ok_With_VoiceChapterStatusResponse()
        {
            // Arrange
            var expectedResponse = new VoiceChapterStatusResponse
            {
                ChapterId = _chapterId,
                CharCount = 1000,
                GenerationCostPerVoiceDias = 2
            };
            _voiceChapterServiceMock.Setup(s => s.GetAsync(_authorId, _chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetStatus(_chapterId, CancellationToken.None);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedResponse);
        }

        [Fact]
        public async Task OrderVoices_Should_Return_Ok_With_VoiceChapterOrderResponse()
        {
            // Arrange
            var request = new VoiceChapterOrderRequest { VoiceIds = new List<Guid> { Guid.NewGuid() } };
            var expectedResponse = new VoiceChapterOrderResponse
            {
                ChapterId = _chapterId,
                TotalGenerationCostDias = 2,
                AuthorRevenueBalanceAfter = 98
            };
            _voiceChapterServiceMock.Setup(s => s.OrderVoicesAsync(_authorId, _chapterId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.Order(_chapterId, request, CancellationToken.None);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedResponse);
        }

        [Fact]
        public async Task GetCharCount_Should_Return_Ok_With_VoiceChapterCharCountResponse()
        {
            // Arrange
            var expectedResponse = new VoiceChapterCharCountResponse
            {
                ChapterId = _chapterId,
                CharacterCount = 1000,
                WordCount = 200
            };
            _voiceChapterServiceMock.Setup(s => s.GetCharCountAsync(_authorId, _chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCharCount(_chapterId, CancellationToken.None);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedResponse);
        }

        [Fact]
        public async Task GetVoiceList_Should_Return_Ok_With_VoicePresetList()
        {
            // Arrange
            var expectedResponse = new[] { new VoicePresetResponse { VoiceId = Guid.NewGuid(), VoiceName = "Voice 1" } };
            _voiceChapterServiceMock.Setup(s => s.GetPresetsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetVoiceList(CancellationToken.None);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedResponse);
        }

        [Fact]
        public async Task GetPricingRules_Should_Return_Ok_With_VoicePricingRuleList()
        {
            // Arrange
            var expectedRules = new[] { new VoicePricingRuleResponse { MinCharCount = 0, SellingPriceDias = 5, GenerationCostDias = 1 } };
            _voicePricingServiceMock.Setup(s => s.GetAllRulesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRules);

            // Act
            var result = await _controller.GetPricingRules(CancellationToken.None);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedRules);
        }
    }
}

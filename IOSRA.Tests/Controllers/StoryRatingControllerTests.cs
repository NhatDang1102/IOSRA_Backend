using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Response.Story; // Added missing using
using Contract.DTOs.Response.Common;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class StoryRatingControllerTests
    {
        private readonly Mock<IStoryRatingService> _serviceMock;
        private readonly StoryRatingController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public StoryRatingControllerTests()
        {
            _serviceMock = new Mock<IStoryRatingService>();
            _controller = new StoryRatingController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task Get_Should_Return_Ok()
        {
            var storyId = Guid.NewGuid();
            var res = new StoryRatingSummaryResponse();
            _serviceMock.Setup(s => s.GetAsync(storyId, _userId, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Get(storyId, 1, 20, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Upsert_Should_Return_Ok()
        {
            var storyId = Guid.NewGuid();
            var req = new StoryRatingRequest();
            var res = new StoryRatingItemResponse();
            _serviceMock.Setup(s => s.UpsertAsync(_userId, storyId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Upsert(storyId, req, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Delete_Should_Return_NoContent()
        {
            var storyId = Guid.NewGuid();
            _serviceMock.Setup(s => s.RemoveAsync(_userId, storyId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.Delete(storyId, CancellationToken.None);
            result.Should().BeOfType<NoContentResult>();
        }
    }
}

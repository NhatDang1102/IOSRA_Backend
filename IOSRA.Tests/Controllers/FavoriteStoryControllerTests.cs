using System;
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
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class FavoriteStoryControllerTests
    {
        private readonly Mock<IFavoriteStoryService> _serviceMock;
        private readonly FavoriteStoryController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public FavoriteStoryControllerTests()
        {
            _serviceMock = new Mock<IFavoriteStoryService>();
            _controller = new FavoriteStoryController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task List_Should_Return_Ok()
        {
            var res = new PagedResult<FavoriteStoryResponse> { Items = Array.Empty<FavoriteStoryResponse>(), Total = 0, Page = 1, PageSize = 20 };
            _serviceMock.Setup(s => s.ListAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.List(1, 20, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Add_Should_Return_Ok()
        {
            var storyId = Guid.NewGuid();
            var res = new FavoriteStoryResponse();
            _serviceMock.Setup(s => s.AddAsync(_userId, storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Add(storyId, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Remove_Should_Return_NoContent()
        {
            var storyId = Guid.NewGuid();
            _serviceMock.Setup(s => s.RemoveAsync(_userId, storyId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.Remove(storyId, CancellationToken.None);
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task Toggle_Should_Return_Ok()
        {
            var storyId = Guid.NewGuid();
            var req = new FavoriteStoryNotificationRequest();
            var res = new FavoriteStoryResponse();
            _serviceMock.Setup(s => s.ToggleNotificationAsync(_userId, storyId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Toggle(storyId, req, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

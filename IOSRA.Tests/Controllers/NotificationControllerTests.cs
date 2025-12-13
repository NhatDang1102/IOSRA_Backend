using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Notification;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Notification;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class NotificationControllerTests
    {
        private readonly Mock<INotificationService> _serviceMock;
        private readonly NotificationController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public NotificationControllerTests()
        {
            _serviceMock = new Mock<INotificationService>();
            _controller = new NotificationController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task GetMine_Should_Return_Ok()
        {
            var res = new PagedResult<NotificationResponse> { Items = Array.Empty<NotificationResponse>(), Total = 0, Page = 1, PageSize = 20 };
            _serviceMock.Setup(s => s.GetAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.List(1, 20, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task MarkRead_Should_Return_NoContent()
        {
            var id = Guid.NewGuid();
            _serviceMock.Setup(s => s.MarkReadAsync(_userId, id, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.MarkRead(id, CancellationToken.None);
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task MarkAllRead_Should_Return_NoContent()
        {
            _serviceMock.Setup(s => s.MarkAllReadAsync(_userId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.MarkAllRead(CancellationToken.None);
            result.Should().BeOfType<NoContentResult>();
        }
    }
}

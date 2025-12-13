using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Subscription;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class SubscriptionControllerTests
    {
        private readonly Mock<ISubscriptionService> _serviceMock;
        private readonly SubscriptionController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public SubscriptionControllerTests()
        {
            _serviceMock = new Mock<ISubscriptionService>();
            _controller = new SubscriptionController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task GetPlans_Should_Return_Ok()
        {
            var res = new[] { new SubscriptionPlanResponse() };
            _serviceMock.Setup(s => s.GetPlansAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetPlans(CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task GetStatus_Should_Return_Ok()
        {
            var res = new SubscriptionStatusResponse();
            _serviceMock.Setup(s => s.GetStatusAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetStatus(CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task ClaimDaily_Should_Return_Ok()
        {
            var res = new SubscriptionClaimResponse();
            _serviceMock.Setup(s => s.ClaimDailyAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.ClaimDaily(CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

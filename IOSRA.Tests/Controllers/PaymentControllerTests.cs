using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Payment;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class PaymentControllerTests
    {
        private readonly Mock<IPaymentService> _serviceMock;
        private readonly Mock<ILogger<PaymentController>> _loggerMock;
        private readonly PaymentController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public PaymentControllerTests()
        {
            _serviceMock = new Mock<IPaymentService>();
            _loggerMock = new Mock<ILogger<PaymentController>>();
            _controller = new PaymentController(_serviceMock.Object, _loggerMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task GetDiaPricing_Should_Return_Ok()
        {
            var res = new[] { new DiaTopupPricingResponse() };
            _serviceMock.Setup(s => s.GetDiaTopupPricingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetDiaPricing(CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

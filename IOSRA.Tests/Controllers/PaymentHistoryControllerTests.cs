using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Payment;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class PaymentHistoryControllerTests
    {
        private readonly Mock<IPaymentHistoryService> _serviceMock;
        private readonly PaymentHistoryController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public PaymentHistoryControllerTests()
        {
            _serviceMock = new Mock<IPaymentHistoryService>();
            _controller = new PaymentHistoryController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task Get_Should_Return_Ok()
        {
            var query = new PaymentHistoryQuery();
            var res = new PagedResult<PaymentHistoryItemResponse> { Items = Array.Empty<PaymentHistoryItemResponse>(), Total = 0, Page = 1, PageSize = 10 };
            _serviceMock.Setup(s => s.GetAsync(_userId, It.IsAny<PaymentHistoryQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Get(query, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

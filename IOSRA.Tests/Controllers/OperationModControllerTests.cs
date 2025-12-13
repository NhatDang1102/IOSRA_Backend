using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.OperationMod;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class OperationModControllerTests
    {
        private readonly Mock<IOperationModService> _serviceMock;
        private readonly Mock<IAuthorRankPromotionService> _rankServiceMock;
        private readonly OperationModController _controller;
        private readonly Guid _omodId = Guid.NewGuid();

        public OperationModControllerTests()
        {
            _serviceMock = new Mock<IOperationModService>();
            _rankServiceMock = new Mock<IAuthorRankPromotionService>();
            _controller = new OperationModController(_serviceMock.Object, _rankServiceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_omodId);
        }

        [Fact]
        public async Task List_Should_Return_Ok()
        {
            var res = new List<OpRequestItemResponse>();
            _serviceMock.Setup(x => x.ListAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.List(null, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task ApproveWithdraw_Should_Return_Ok()
        {
            var reqId = Guid.NewGuid();
            var request = new ApproveWithdrawRequest { Note = "OK" };
            
            _serviceMock.Setup(x => x.ApproveWithdrawAsync(reqId, _omodId, request, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.ApproveWithdraw(reqId, request, CancellationToken.None);
            result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task RejectWithdraw_Should_Return_Ok()
        {
            var reqId = Guid.NewGuid();
            var request = new RejectWithdrawRequest { Note = "No" };

            _serviceMock.Setup(x => x.RejectWithdrawAsync(reqId, _omodId, request, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _controller.RejectWithdraw(reqId, request, CancellationToken.None);
            result.Should().BeOfType<OkObjectResult>();
        }
    }
}

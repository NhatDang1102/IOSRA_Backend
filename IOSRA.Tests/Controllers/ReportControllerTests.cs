using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Report;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Report;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class ReportControllerTests
    {
        private readonly Mock<IReportService> _serviceMock;
        private readonly ReportController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public ReportControllerTests()
        {
            _serviceMock = new Mock<IReportService>();
            _controller = new ReportController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task Create_Should_Return_Ok()
        {
            var req = new ReportCreateRequest();
            var res = new ReportResponse();
            _serviceMock.Setup(s => s.CreateAsync(_userId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Create(req, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task GetMyReports_Should_Return_Ok()
        {
            var res = new PagedResult<ReportResponse> { Items = Array.Empty<ReportResponse>(), Total = 0, Page = 1, PageSize = 20 };
            _serviceMock.Setup(s => s.GetMyReportsAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetMyReports(1, 20, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

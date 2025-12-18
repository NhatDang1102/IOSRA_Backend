using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Admin;
using Contract.DTOs.Response.Common;
using FluentAssertions;
using Main.Controllers.Main.Controllers; // Fix namespace later if needed
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class AdminControllerTests
    {
        private readonly Mock<IAdminService> _serviceMock;
        private readonly AdminController _controller;
        private readonly Guid _adminId = Guid.NewGuid();

        public AdminControllerTests()
        {
            _serviceMock = new Mock<IAdminService>();
            _controller = new AdminController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_adminId);
        }

        [Fact]
        public async Task GetAccounts_Should_Return_Ok()
        {
            var res = new PagedResult<AdminAccountResponse> { Items = Array.Empty<AdminAccountResponse>(), Total = 0, Page = 1, PageSize = 20 };
            _serviceMock.Setup(x => x.GetAccountsAsync(null, null, null, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetAccounts(null, null, null, 1, 20, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task CreateContentMod_Should_Return_Ok()
        {
            var req = new CreateModeratorRequest();
            var res = new AdminAccountResponse();
            _serviceMock.Setup(x => x.CreateContentModAsync(req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.CreateContentMod(req, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task UpdateStatus_Should_Return_Ok()
        {
            var accId = Guid.NewGuid();
            var req = new UpdateAccountStatusRequest();
            var res = new AdminAccountResponse();
            _serviceMock.Setup(x => x.UpdateStatusAsync(accId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.UpdateStatus(accId, req, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

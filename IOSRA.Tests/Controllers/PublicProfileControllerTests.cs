using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Profile;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class PublicProfileControllerTests
    {
        private readonly Mock<IPublicProfileService> _serviceMock;
        private readonly Mock<IStoryCatalogService> _catalogMock;
        private readonly PublicProfileController _controller;
        private readonly Guid _viewerId = Guid.NewGuid();

        public PublicProfileControllerTests()
        {
            _serviceMock = new Mock<IPublicProfileService>();
            _catalogMock = new Mock<IStoryCatalogService>();
            _controller = new PublicProfileController(_serviceMock.Object, _catalogMock.Object);
            _controller.ControllerContext = CreateControllerContext(_viewerId);
        }

        [Fact]
        public async Task GetProfile_Should_Return_Ok()
        {
            var targetId = Guid.NewGuid();
            var res = new PublicProfileResponse();
            _serviceMock.Setup(x => x.GetAsync(_viewerId, targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetProfile(targetId, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

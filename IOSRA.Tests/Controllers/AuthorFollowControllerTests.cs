using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Follow;
using Contract.DTOs.Response.Follow;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class AuthorFollowControllerTests
    {
        private readonly Mock<IAuthorFollowService> _serviceMock;
        private readonly AuthorFollowController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public AuthorFollowControllerTests()
        {
            _serviceMock = new Mock<IAuthorFollowService>();
            _controller = new AuthorFollowController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task Follow_Should_Return_Ok()
        {
            var authorId = Guid.NewGuid();
            var req = new AuthorFollowRequest();
            _serviceMock.Setup(x => x.FollowAsync(_userId, authorId, It.IsAny<AuthorFollowRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthorFollowStatusResponse());

            var result = await _controller.Follow(authorId, req, CancellationToken.None);
            result.Should().BeOfType<OkObjectResult>();
        }
    }
}

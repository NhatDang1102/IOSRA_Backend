using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Moderation;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class ChapterCommentModerationControllerTests
    {
        private readonly Mock<IChapterCommentService> _serviceMock;
        private readonly ChapterCommentModerationController _controller;
        private readonly Guid _modId = Guid.NewGuid();

        public ChapterCommentModerationControllerTests()
        {
            _serviceMock = new Mock<IChapterCommentService>();
            _controller = new ChapterCommentModerationController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_modId);
        }

        [Fact]
        public async Task List_Should_Return_Ok()
        {
            var res = new PagedResult<ChapterCommentModerationResponse>
            {
                Items = Array.Empty<ChapterCommentModerationResponse>(),
                Total = 0,
                Page = 1,
                PageSize = 20
            };
            _serviceMock.Setup(s => s.GetForModerationAsync(null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.List(null, null, null, null, 1, 20, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

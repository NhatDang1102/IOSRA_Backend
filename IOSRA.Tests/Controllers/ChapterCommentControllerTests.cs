using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Chapter;
using Contract.DTOs.Response.Chapter;
using Contract.DTOs.Response.Common;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class ChapterCommentControllerTests
    {
        private readonly Mock<IChapterCommentService> _serviceMock;
        private readonly ChapterCommentController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public ChapterCommentControllerTests()
        {
            _serviceMock = new Mock<IChapterCommentService>();
            _controller = new ChapterCommentController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task GetByChapter_Should_Return_Ok()
        {
            var chapterId = Guid.NewGuid();
            var res = new PagedResult<ChapterCommentResponse> { Items = Array.Empty<ChapterCommentResponse>(), Total = 0, Page = 1, PageSize = 20 };
            _serviceMock.Setup(s => s.GetByChapterAsync(chapterId, 1, 20, It.IsAny<CancellationToken>(), _userId))
                .ReturnsAsync(res);

            var result = await _controller.GetByChapter(chapterId, 1, 20, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Add_Should_Return_Ok()
        {
            var chapterId = Guid.NewGuid();
            var req = new ChapterCommentCreateRequest { Content = "Test Comment" }; 
            var res = new ChapterCommentResponse();
            _serviceMock.Setup(s => s.CreateAsync(_userId, chapterId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Create(chapterId, req, CancellationToken.None); 
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

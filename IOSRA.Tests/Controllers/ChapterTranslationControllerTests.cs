using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Chapter;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class ChapterTranslationControllerTests
    {
        private readonly Mock<IChapterTranslationService> _serviceMock;
        private readonly ChapterTranslationController _controller;

        public ChapterTranslationControllerTests()
        {
            _serviceMock = new Mock<IChapterTranslationService>();
            _controller = new ChapterTranslationController(_serviceMock.Object);
        }

        [Fact]
        public async Task GetTranslation_Should_Return_Ok()
        {
            var chapterId = Guid.NewGuid();
            var res = new ChapterTranslationResponse();
            _serviceMock.Setup(x => x.GetAsync(chapterId, "vi", null, It.IsAny<CancellationToken>())) // Assuming TryGetAccountId returns null
                .ReturnsAsync(res);

            var result = await _controller.Get(chapterId, "vi", CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

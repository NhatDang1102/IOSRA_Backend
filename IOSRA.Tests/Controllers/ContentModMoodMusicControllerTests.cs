using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.ContentMod;
using Contract.DTOs.Response.ContentMod;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class ContentModMoodMusicControllerTests
    {
        private readonly Mock<IMoodMusicService> _serviceMock;
        private readonly ContentModMoodMusicController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public ContentModMoodMusicControllerTests()
        {
            _serviceMock = new Mock<IMoodMusicService>();
            _controller = new ContentModMoodMusicController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task List_Should_Return_Ok()
        {
            var res = new List<MoodTrackResponse>();
            _serviceMock.Setup(x => x.GetTracksAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetTracks(null, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

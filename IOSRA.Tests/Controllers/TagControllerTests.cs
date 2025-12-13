using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Tag;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Tag;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class TagControllerTests
    {
        private readonly Mock<ITagService> _serviceMock;
        private readonly TagController _controller;

        public TagControllerTests()
        {
            _serviceMock = new Mock<ITagService>();
            _controller = new TagController(_serviceMock.Object);
        }

        [Fact]
        public async Task GetAll_Should_Return_Ok()
        {
            var res = new List<TagResponse>();
            _serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetAll(CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Top_Should_Return_Ok()
        {
            var res = new List<TagOptionResponse>();
            _serviceMock.Setup(s => s.GetTopOptionsAsync(50, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Top(50, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task GetPaged_Should_Return_Ok()
        {
            var res = new PagedResult<TagPagedItem> { Items = Array.Empty<TagPagedItem>(), Total = 0, Page = 1, PageSize = 20 };
            _serviceMock.Setup(s => s.GetPagedAsync(null, "name", true, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.GetPaged(null, true, 1, 20, CancellationToken.None);
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Create_Should_Return_Ok()
        {
            var req = new TagCreateRequest();
            var res = new TagResponse();
            _serviceMock.Setup(s => s.CreateAsync(req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Create(req, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }

        [Fact]
        public async Task Update_Should_Return_Ok()
        {
            var id = Guid.NewGuid();
            var req = new TagUpdateRequest();
            var res = new TagResponse();
            _serviceMock.Setup(s => s.UpdateAsync(id, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.Update(id, req, CancellationToken.None);
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().Be(res);
        }
    }
}

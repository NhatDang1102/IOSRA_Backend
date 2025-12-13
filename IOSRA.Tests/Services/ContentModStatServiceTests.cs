using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.Common;
using FluentAssertions;
using Moq;
using Repository.DataModels;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class ContentModStatServiceTests
    {
        private readonly Mock<IContentModStatRepository> _repoMock;
        private readonly ContentModStatService _service;

        public ContentModStatServiceTests()
        {
            _repoMock = new Mock<IContentModStatRepository>();
            _service = new ContentModStatService(_repoMock.Object);
        }

        [Fact]
        public async Task GetStoryPublishStatsAsync_Should_Return_Series()
        {
            var points = new List<StatPointData> { new() { Value = 10, RangeStart = DateTime.Now, RangeEnd = DateTime.Now } };
            _repoMock.Setup(x => x.GetPublishedStoriesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), "month", It.IsAny<CancellationToken>()))
                .ReturnsAsync(points);

            var res = await _service.GetStoryPublishStatsAsync(new StatQueryRequest { Period = "month" });

            res.Total.Should().Be(10);
            res.Points.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetStoryPublishStatsAsync_Should_Throw_If_Invalid_Period()
        {
            await Assert.ThrowsAsync<AppException>(() => _service.GetStoryPublishStatsAsync(new StatQueryRequest { Period = "invalid" }));
        }
    }
}

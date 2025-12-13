using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Common;
using Contract.DTOs.Response.OperationMod;
using FluentAssertions;
using Moq;
using Repository.DataModels;
using Repository.Interfaces;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class OperationModStatServiceTests
    {
        private readonly Mock<IOperationModStatRepository> _repoMock;
        private readonly OperationModStatService _service;

        public OperationModStatServiceTests()
        {
            _repoMock = new Mock<IOperationModStatRepository>();
            _service = new OperationModStatService(_repoMock.Object);
        }

        [Fact]
        public async Task GetRevenueStatsAsync_Should_Return_Response()
        {
            var data = new OperationRevenueData 
            { 
                DiaTopup = 100, 
                Points = new List<StatPointData>() 
            };
            _repoMock.Setup(x => x.GetRevenueAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), "month", It.IsAny<CancellationToken>()))
                .ReturnsAsync(data);

            var res = await _service.GetRevenueStatsAsync(new StatQueryRequest { Period = "month" });

            res.DiaTopup.Should().Be(100);
        }
    }
}

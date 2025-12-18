using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Subscription;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class SubscriptionServiceTests
    {
        private readonly Mock<ISubscriptionRepository> _repoMock;
        private readonly Mock<ILogger<SubscriptionService>> _loggerMock;
        private readonly SubscriptionService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public SubscriptionServiceTests()
        {
            _repoMock = new Mock<ISubscriptionRepository>();
            _loggerMock = new Mock<ILogger<SubscriptionService>>();
            _service = new SubscriptionService(_repoMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetPlansAsync_Should_Return_List()
        {
            var plans = new List<subscription_plan>
            {
                new() { plan_code = "P1", plan_name = "Plan 1", price_vnd = 1000 }
            };
            _repoMock.Setup(x => x.GetPlansAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(plans);

            var res = await _service.GetPlansAsync();
            res.Should().HaveCount(1);
            res[0].PlanCode.Should().Be("P1");
        }

        [Fact]
        public async Task GetStatusAsync_Should_Return_False_When_No_Sub()
        {
            _repoMock.Setup(x => x.GetLatestActiveAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((subscription?)null);

            var res = await _service.GetStatusAsync(_userId);
            res.HasActiveSubscription.Should().BeFalse();
        }

        [Fact]
        public async Task ClaimDailyAsync_Should_Fail_If_No_Sub()
        {
            _repoMock.Setup(x => x.GetLatestActiveAsync(_userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((subscription?)null);

            await Assert.ThrowsAsync<AppException>(() => _service.ClaimDailyAsync(_userId));
        }

        [Fact]
        public async Task ActivateSubscriptionAsync_Should_Fail_If_Plan_Not_Found()
        {
            _repoMock.Setup(x => x.GetPlanAsync("INVALID", It.IsAny<CancellationToken>()))
                .ReturnsAsync((subscription_plan?)null);

            await Assert.ThrowsAsync<AppException>(() => _service.ActivateSubscriptionAsync(_userId, "INVALID"));
        }
    }
}

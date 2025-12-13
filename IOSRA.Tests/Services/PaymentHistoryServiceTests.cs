using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Payment;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Payment;
using FluentAssertions;
using Moq;
using Repository.DataModels;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class PaymentHistoryServiceTests
    {
        private readonly Mock<IPaymentHistoryRepository> _repoMock;
        private readonly PaymentHistoryService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public PaymentHistoryServiceTests()
        {
            _repoMock = new Mock<IPaymentHistoryRepository>();
            _service = new PaymentHistoryService(_repoMock.Object);
        }

        [Fact]
        public async Task GetAsync_Should_Return_PagedResult()
        {
            var query = new PaymentHistoryQuery { Page = 1, PageSize = 10 };
            var items = new List<PaymentHistoryRecord>
            {
                new() { PaymentId = Guid.NewGuid(), AmountVnd = 1000 }
            };
            _repoMock.Setup(x => x.GetHistoryAsync(_userId, 1, 10, null, null, null, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync((items, 1));

            var result = await _service.GetAsync(_userId, query);

            result.Total.Should().Be(1);
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetAsync_Should_Throw_If_Page_Invalid()
        {
            var query = new PaymentHistoryQuery { Page = 0 };
            await Assert.ThrowsAsync<AppException>(() => _service.GetAsync(_userId, query));
        }
    }
}

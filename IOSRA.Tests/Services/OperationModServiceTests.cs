using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.OperationMod;
using Contract.DTOs.Response.OperationMod;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Implementations;
using Service.Interfaces;
using Service.Models;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class OperationModServiceTests
    {
        private readonly Mock<IOpRequestRepository> _opRepoMock;
        private readonly Mock<INotificationService> _notiMock;
        private readonly Mock<IAuthorRevenueRepository> _revenueRepoMock;
        private readonly OperationModService _service;
        private readonly Guid _omodId = Guid.NewGuid();

        public OperationModServiceTests()
        {
            _opRepoMock = new Mock<IOpRequestRepository>();
            _notiMock = new Mock<INotificationService>();
            _revenueRepoMock = new Mock<IAuthorRevenueRepository>();
            _service = new OperationModService(_opRepoMock.Object, _notiMock.Object, _revenueRepoMock.Object);

            // Mock transaction
            _revenueRepoMock.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);
        }

        [Fact]
        public async Task ListAsync_Should_Return_Mapped_Items()
        {
            var requests = new List<op_request>
            {
                new() { request_id = Guid.NewGuid(), request_content = "{}" }
            };
            _opRepoMock.Setup(x => x.ListRequestsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(requests);

            var result = await _service.ListAsync(null);
            result.Should().HaveCount(1);
        }

        [Fact]
        public async Task ApproveWithdrawAsync_Should_Update_Status_And_Revenue()
        {
            var reqId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var amount = 1000UL;
            
            var opRequest = new op_request 
            { 
                request_id = reqId, 
                requester_id = authorId, 
                status = "pending", 
                withdraw_amount = amount 
            };

            var author = new author 
            { 
                account_id = authorId, 
                revenue_pending = (long)amount, 
                revenue_withdrawn = 0 
            };

            _opRepoMock.Setup(x => x.GetWithdrawRequestAsync(reqId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(opRequest);
            _revenueRepoMock.Setup(x => x.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(author);

            await _service.ApproveWithdrawAsync(reqId, _omodId, new ApproveWithdrawRequest { Note = "OK" });

            // Verify
            author.revenue_pending.Should().Be(0);
            author.revenue_withdrawn.Should().Be((long)amount);
            opRequest.status.Should().Be("approved");
            opRequest.omod_id.Should().Be(_omodId);

            _opRepoMock.Verify(x => x.UpdateOpRequestAsync(opRequest, It.IsAny<CancellationToken>()), Times.Once);
            _revenueRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _notiMock.Verify(x => x.CreateAsync(It.IsAny<NotificationCreateModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RejectWithdrawAsync_Should_Refund_Revenue_To_Balance()
        {
            var reqId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var amount = 1000UL;

            var opRequest = new op_request
            {
                request_id = reqId,
                requester_id = authorId,
                status = "pending",
                withdraw_amount = amount
            };

            var author = new author
            {
                account_id = authorId,
                revenue_pending = (long)amount,
                revenue_balance = 0
            };

            _opRepoMock.Setup(x => x.GetWithdrawRequestAsync(reqId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(opRequest);
            _revenueRepoMock.Setup(x => x.GetAuthorAsync(authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(author);

            await _service.RejectWithdrawAsync(reqId, _omodId, new RejectWithdrawRequest { Note = "Bad" });

            // Verify
            author.revenue_pending.Should().Be(0);
            author.revenue_balance.Should().Be((long)amount); // Refunded
            opRequest.status.Should().Be("rejected");

            _revenueRepoMock.Verify(x => x.AddTransactionAsync(
                It.Is<author_revenue_transaction>(t => t.type == "withdraw_release"), 
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

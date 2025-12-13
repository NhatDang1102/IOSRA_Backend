using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class AuthorRevenueServiceTests
    {
        private readonly Mock<IAuthorRevenueRepository> _revenueRepoMock;
        private readonly Mock<IOpRequestRepository> _opReqRepoMock;
        private readonly AuthorRevenueService _service;

        private readonly Guid _authorId = Guid.NewGuid();

        public AuthorRevenueServiceTests()
        {
            _revenueRepoMock = new Mock<IAuthorRevenueRepository>();
            _opReqRepoMock = new Mock<IOpRequestRepository>();
            _service = new AuthorRevenueService(_revenueRepoMock.Object, _opReqRepoMock.Object);

            // Mock transaction
            _revenueRepoMock.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<IDbContextTransaction>().Object);
        }

        [Fact]
        public async Task SubmitWithdrawAsync_Should_Succeed_When_Conditions_Met()
        {
            // Arrange
            var currentBalance = 5000;
            var withdrawAmount = 2000;
            
            var author = new author 
            { 
                account_id = _authorId, 
                revenue_balance = currentBalance,
                revenue_pending = 0
            };

            var req = new AuthorWithdrawRequest 
            { 
                Amount = withdrawAmount,
                BankName = "TestBank",
                BankAccountNumber = "123",
                AccountHolderName = "TestUser"
            };

            _revenueRepoMock.Setup(x => x.GetAuthorAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(author);
            
            _opReqRepoMock.Setup(x => x.HasPendingWithdrawRequestAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _opReqRepoMock.Setup(x => x.CreateWithdrawRequestAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new op_request { request_id = Guid.NewGuid(), status = "pending", withdraw_amount = (ulong)withdrawAmount });

            // Act
            var result = await _service.SubmitWithdrawAsync(_authorId, req);

            // Assert
            // 1. Balance phải bị trừ
            author.revenue_balance.Should().Be(currentBalance - withdrawAmount); // 5000 - 2000 = 3000
            
            // 2. Pending phải tăng
            author.revenue_pending.Should().Be(withdrawAmount); // 0 + 2000 = 2000

            // 3. Phải tạo transaction log "withdraw_reserve"
            _revenueRepoMock.Verify(x => x.AddTransactionAsync(
                It.Is<author_revenue_transaction>(t => t.type == "withdraw_reserve" && t.amount == -withdrawAmount), 
                It.IsAny<CancellationToken>()), Times.Once);

            // 4. Phải lưu DB
            _revenueRepoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SubmitWithdrawAsync_Should_Fail_If_Balance_Insufficient()
        {
            // Arrange
            var author = new author { account_id = _authorId, revenue_balance = 1000 }; // Có 1000
            var req = new AuthorWithdrawRequest { Amount = 2000 }; // Đòi rút 2000

            _revenueRepoMock.Setup(x => x.GetAuthorAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(author);

            // Act & Assert
            await Assert.ThrowsAsync<AppException>(() => _service.SubmitWithdrawAsync(_authorId, req));
        }

        [Fact]
        public async Task SubmitWithdrawAsync_Should_Fail_If_Pending_Request_Exists()
        {
            // Arrange
            var author = new author { account_id = _authorId, revenue_balance = 5000 };
            var req = new AuthorWithdrawRequest { Amount = 2000 };

            _revenueRepoMock.Setup(x => x.GetAuthorAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(author);
            
            _opReqRepoMock.Setup(x => x.HasPendingWithdrawRequestAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true); // Đang có request pending

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() => _service.SubmitWithdrawAsync(_authorId, req));
            ex.Message.Should().Contain("pending");
        }

        [Fact]
        public async Task SubmitWithdrawAsync_Should_Fail_If_Amount_Too_Small()
        {
            // Arrange (Min 1000)
            var req = new AuthorWithdrawRequest { Amount = 500 }; 

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() => _service.SubmitWithdrawAsync(_authorId, req));
            ex.ErrorCode.Should().Be("AmountTooSmall");
        }
    }
}

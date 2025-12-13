using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Chapter;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class ChapterPurchaseServiceTests
    {
        private readonly Mock<IChapterPurchaseRepository> _purchaseRepoMock;
        private readonly Mock<IBillingRepository> _billingRepoMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly Mock<IProfileRepository> _profileRepoMock;
        private readonly Mock<ILogger<ChapterPurchaseService>> _loggerMock;
        private readonly ChapterPurchaseService _service;

        private readonly Guid _readerId = Guid.NewGuid();
        private readonly Guid _authorId = Guid.NewGuid();
        private readonly Guid _chapterId = Guid.NewGuid();

        public ChapterPurchaseServiceTests()
        {
            _purchaseRepoMock = new Mock<IChapterPurchaseRepository>();
            _billingRepoMock = new Mock<IBillingRepository>();
            _notificationMock = new Mock<INotificationService>();
            _profileRepoMock = new Mock<IProfileRepository>();
            _loggerMock = new Mock<ILogger<ChapterPurchaseService>>();

            _service = new ChapterPurchaseService(
                _purchaseRepoMock.Object,
                _billingRepoMock.Object,
                _notificationMock.Object,
                _profileRepoMock.Object,
                _loggerMock.Object);
            
            // Mock transaction
            _purchaseRepoMock.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);
        }

        [Fact]
        public async Task PurchaseAsync_Should_Transfer_100_Percent_Dias_To_Author()
        {
            // Arrange
            var price = 10;
            var readerBalance = 50;
            var authorRevenue = 100;

            var chapter = new chapter 
            { 
                chapter_id = _chapterId, 
                chapter_no = 1,
                title = "Test Chapter",
                dias_price = (uint)price, 
                access_type = "dias",
                status = "published",
                story = new story 
                { 
                    status = "published", 
                    author_id = _authorId,
                    author = new author 
                    { 
                        account_id = _authorId, 
                        revenue_balance = authorRevenue,
                        account = new account { username = "author" }
                    }
                } 
            };

            var wallet = new dia_wallet 
            { 
                wallet_id = Guid.NewGuid(), 
                balance_dias = readerBalance 
            };

            _purchaseRepoMock.Setup(x => x.GetChapterForPurchaseAsync(_chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chapter);
            _purchaseRepoMock.Setup(x => x.HasReaderPurchasedChapterAsync(_chapterId, _readerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _billingRepoMock.Setup(x => x.GetOrCreateDiaWalletAsync(_readerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(wallet);
            _profileRepoMock.Setup(x => x.GetAccountByIdAsync(_readerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new account { account_id = _readerId, username = "reader" });

            // Act
            var result = await _service.PurchaseAsync(_readerId, _chapterId);

            // Assert
            // 1. Reader bị trừ tiền
            wallet.balance_dias.Should().Be(readerBalance - price); // 50 - 10 = 40
            
            // 2. Author được cộng đủ 100% (10 Dias)
            chapter.story.author.revenue_balance.Should().Be(authorRevenue + price); // 100 + 10 = 110

            // 3. Verify Log
            _purchaseRepoMock.Verify(x => x.AddPurchaseLogAsync(It.IsAny<chapter_purchase_log>(), It.IsAny<CancellationToken>()), Times.Once);
            _billingRepoMock.Verify(x => x.AddWalletPaymentAsync(
                It.Is<wallet_payment>(p => p.dias_delta == -price && p.type == "purchase"), 
                It.IsAny<CancellationToken>()), Times.Once);
            
            _purchaseRepoMock.Verify(x => x.AddAuthorRevenueTransactionAsync(
                It.Is<author_revenue_transaction>(t => t.amount == price && t.type == "purchase"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PurchaseAsync_Should_Fail_If_Balance_Insufficient()
        {
            // Arrange
            var price = 100;
            var readerBalance = 10; // Không đủ

            var chapter = new chapter
            {
                chapter_id = _chapterId,
                status = "published",
                access_type = "dias",
                dias_price = (uint)price,
                story = new story { status = "published", author_id = _authorId }
            };

            var wallet = new dia_wallet { balance_dias = readerBalance };

            _purchaseRepoMock.Setup(x => x.GetChapterForPurchaseAsync(_chapterId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(chapter);
            _billingRepoMock.Setup(x => x.GetOrCreateDiaWalletAsync(_readerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(wallet);

            // Act
            var act = () => _service.PurchaseAsync(_readerId, _chapterId);

            // Assert
            await act.Should().ThrowAsync<AppException>()
                .WithMessage("*Not enough dias*");
        }
    }
}

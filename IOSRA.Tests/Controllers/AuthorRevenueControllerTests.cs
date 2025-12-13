using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Response.Author;
using Contract.DTOs.Response.Common;
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
    public class AuthorRevenueControllerTests
    {
        private readonly Mock<IAuthorRevenueService> _serviceMock;
        private readonly AuthorRevenueController _controller;
        private readonly Guid _authorId = Guid.NewGuid();

        public AuthorRevenueControllerTests()
        {
            _serviceMock = new Mock<IAuthorRevenueService>();
            _controller = new AuthorRevenueController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_authorId);
        }

        [Fact]
        public async Task GetSummary_Should_Return_Ok_With_Summary()
        {
            // Arrange
            var expectedSummary = new AuthorRevenueSummaryResponse
            {
                RevenueBalance = 1000,
                RevenuePending = 500,
                TotalRevenue = 1500
            };
            _serviceMock.Setup(s => s.GetSummaryAsync(_authorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSummary);

            // Act
            var result = await _controller.GetSummary(CancellationToken.None);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedSummary);
        }

        [Fact]
        public async Task GetTransactions_Should_Return_Ok_With_PagedResult()
        {
            // Arrange
            var query = new AuthorRevenueTransactionQuery { Page = 1, PageSize = 10 };
            var expectedPagedResult = new PagedResult<AuthorRevenueTransactionItemResponse>
            {
                Items = new[] { new AuthorRevenueTransactionItemResponse { TransactionId = Guid.NewGuid(), Amount = 100 } },
                Total = 1,
                Page = query.Page,
                PageSize = query.PageSize
            };
            _serviceMock.Setup(s => s.GetTransactionsAsync(_authorId, query, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPagedResult);

            // Act
            var result = await _controller.GetTransactions(query, CancellationToken.None);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedPagedResult);
        }

        [Fact]
        public async Task SubmitWithdraw_Should_Return_Ok_On_Success()
        {
            // Arrange
            var request = new AuthorWithdrawRequest { Amount = 1000, BankName = "Test", AccountHolderName = "Test", BankAccountNumber = "123" };
            var expectedResponse = new AuthorWithdrawRequestResponse { RequestId = Guid.NewGuid(), Amount = 1000, Status = "pending" };
            
            _serviceMock.Setup(s => s.SubmitWithdrawAsync(_authorId, request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SubmitWithdraw(request, CancellationToken.None);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedResponse);
        }

        [Fact]
        public async Task ListWithdrawRequests_Should_Return_Ok_With_Requests()
        {
            // Arrange
            var expectedRequests = new[] { new AuthorWithdrawRequestResponse { RequestId = Guid.NewGuid(), Amount = 1000 } };
            _serviceMock.Setup(s => s.GetWithdrawRequestsAsync(_authorId, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedRequests);

            // Act
            var result = await _controller.ListWithdrawRequests(null, CancellationToken.None);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(expectedRequests);
        }
    }
}
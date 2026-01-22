using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Admin;
using Contract.DTOs.Response.Admin;
using Contract.DTOs.Response.Common;
using FluentAssertions;
using Moq;
using Repository.DataModels;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class AdminServiceTests
    {
        private readonly Mock<IAdminRepository> _repoMock;
        private readonly Mock<IMailSender> _mailSenderMock;
        private readonly AdminService _service;

        public AdminServiceTests()
        {
            _repoMock = new Mock<IAdminRepository>();
            _mailSenderMock = new Mock<IMailSender>();
            _service = new AdminService(_repoMock.Object, _mailSenderMock.Object);
        }

        [Fact]
        public async Task GetAccountsAsync_Should_Return_PagedResult()
        {
            var items = new List<AdminAccountProjection>
            {
                new() { AccountId = Guid.NewGuid(), Username = "user1", Roles = new[] { "reader" } }
            };
            _repoMock.Setup(x => x.GetAccountsAsync(null, null, null, 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync((items, 1));

            var result = await _service.GetAccountsAsync(null, null, null, 1, 20);

            result.Should().NotBeNull();
            result.Total.Should().Be(1);
            result.Items.Should().HaveCount(1);
        }

        [Fact]
        public async Task CreateContentModAsync_Should_Create_Account_And_Role()
        {
            var req = new CreateModeratorRequest { Email = "mod@test.com", Username = "mod", Password = "123" };
            
            _repoMock.Setup(x => x.EmailExistsAsync(req.Email, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _repoMock.Setup(x => x.UsernameExistsAsync(req.Username, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            
            _repoMock.Setup(x => x.GetAccountAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AdminAccountProjection { AccountId = Guid.NewGuid(), Roles = new[] { "cmod" } });

            var result = await _service.CreateContentModAsync(req);

            _repoMock.Verify(x => x.AddAccountAsync(It.IsAny<account>(), It.IsAny<CancellationToken>()), Times.Once);
            _repoMock.Verify(x => x.AddRoleAsync(It.IsAny<Guid>(), "cmod", It.IsAny<CancellationToken>()), Times.Once);
            _repoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateStatusAsync_Should_Update_Status()
        {
            var accId = Guid.NewGuid();
            var req = new UpdateAccountStatusRequest { Status = "banned" };
            var account = new AdminAccountProjection { AccountId = accId, Status = "unbanned", Roles = Array.Empty<string>(), Email = "test@test.com", Username = "test" };

            // Mock GetAccountAsync: First call returns current state
            _repoMock.Setup(x => x.GetAccountAsync(accId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            
            // Mock GetAuthorRevenueInfoAsync (Required since we are banning)
            _repoMock.Setup(x => x.GetAuthorRevenueInfoAsync(accId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((false, 0, 0)); // Not an author

            await _service.UpdateStatusAsync(accId, req);

            _repoMock.Verify(x => x.SetAccountStatusAsync(accId, "banned", It.IsAny<CancellationToken>()), Times.Once);
            _repoMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        
        [Fact]
        public async Task UpdateStatusAsync_Should_Send_Email_If_Author_Banned()
        {
            var accId = Guid.NewGuid();
            var req = new UpdateAccountStatusRequest { Status = "banned" };
            var account = new AdminAccountProjection { AccountId = accId, Status = "unbanned", Roles = new[] { "author" }, Email = "author@test.com", Username = "author" };

            _repoMock.Setup(x => x.GetAccountAsync(accId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            // Is Author
            _repoMock.Setup(x => x.GetAuthorRevenueInfoAsync(accId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((true, 100, 50));

            await _service.UpdateStatusAsync(accId, req);

            _repoMock.Verify(x => x.SetAccountStatusAsync(accId, "banned", It.IsAny<CancellationToken>()), Times.Once);
            _mailSenderMock.Verify(x => x.SendAuthorBanNotificationAsync("author@test.com", "author", 100, 50), Times.Once);
        }
    }
}
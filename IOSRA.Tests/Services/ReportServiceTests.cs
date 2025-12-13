using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Report;
using Contract.DTOs.Response.Common;
using Contract.DTOs.Response.Report;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Interfaces;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class ReportServiceTests
    {
        private readonly Mock<IReportRepository> _repoMock;
        private readonly Mock<IModerationRepository> _modRepoMock;
        private readonly Mock<IProfileRepository> _profileRepoMock;
        private readonly Mock<IMailSender> _mailMock;
        private readonly Mock<INotificationService> _notiMock;
        private readonly Mock<IContentModRepository> _cmodRepoMock;
        private readonly ReportService _service;
        private readonly Guid _reporterId = Guid.NewGuid();

        public ReportServiceTests()
        {
            _repoMock = new Mock<IReportRepository>();
            _modRepoMock = new Mock<IModerationRepository>();
            _profileRepoMock = new Mock<IProfileRepository>();
            _mailMock = new Mock<IMailSender>();
            _notiMock = new Mock<INotificationService>();
            _cmodRepoMock = new Mock<IContentModRepository>();

            _service = new ReportService(
                _repoMock.Object, 
                _modRepoMock.Object, 
                _profileRepoMock.Object, 
                _mailMock.Object, 
                _notiMock.Object, 
                _cmodRepoMock.Object);
        }

        [Fact]
        public async Task CreateAsync_Should_Create_Report()
        {
            var req = new ReportCreateRequest 
            { 
                TargetType = "story", 
                TargetId = Guid.NewGuid(), 
                Reason = "spam", 
                Details = "details" 
            };

            var story = new story { author = new author { account = new account { account_id = Guid.NewGuid() } } };
            _modRepoMock.Setup(x => x.GetStoryAsync(req.TargetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(story);
            _profileRepoMock.Setup(x => x.GetAccountByIdAsync(story.author.account.account_id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(story.author.account);
            _repoMock.Setup(x => x.HasPendingReportAsync(_reporterId, "story", req.TargetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            
            _repoMock.Setup(x => x.AddAsync(It.IsAny<report>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new report 
                { 
                    reporter_id = _reporterId, 
                    target_type = "story", 
                    target_id = req.TargetId,
                    reporter = new account { username = "reporter" }
                });

            var res = await _service.CreateAsync(_reporterId, req);

            res.Should().NotBeNull();
            res.TargetType.Should().Be("story");
            _repoMock.Verify(x => x.AddAsync(It.IsAny<report>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_Should_Fail_If_Target_Not_Found()
        {
            var req = new ReportCreateRequest { TargetType = "story", TargetId = Guid.NewGuid(), Reason = "spam" };
            _modRepoMock.Setup(x => x.GetStoryAsync(req.TargetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((story?)null);

            await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(_reporterId, req));
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Moderation;
using Contract.DTOs.Response.Moderation;
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
    public class ContentModHandlingServiceTests
    {
        private readonly Mock<IModerationRepository> _modRepoMock;
        private readonly Mock<IProfileRepository> _profileRepoMock;
        private readonly Mock<IMailSender> _mailMock;
        private readonly ContentModHandlingService _service;
        private readonly Guid _modId = Guid.NewGuid();

        public ContentModHandlingServiceTests()
        {
            _modRepoMock = new Mock<IModerationRepository>();
            _profileRepoMock = new Mock<IProfileRepository>();
            _mailMock = new Mock<IMailSender>();
            _service = new ContentModHandlingService(_modRepoMock.Object, _profileRepoMock.Object, _mailMock.Object);
        }

        [Fact]
        public async Task UpdateStoryStatusAsync_Should_Update_Status()
        {
            var storyId = Guid.NewGuid();
            var story = new story { story_id = storyId, status = "pending" };
            
            _modRepoMock.Setup(x => x.GetStoryAsync(storyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(story);

            var res = await _service.UpdateStoryStatusAsync(_modId, storyId, new ContentStatusUpdateRequest { Status = "published" });

            res.Status.Should().Be("published");
            story.status.Should().Be("published");
            _modRepoMock.Verify(x => x.UpdateStoryAsync(story, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ApplyStrikeAsync_Should_Update_Strike_And_Send_Email()
        {
            var targetId = Guid.NewGuid();
            var account = new account { account_id = targetId, email = "test@test.com", strike = 0 };

            _profileRepoMock.Setup(x => x.GetAccountByIdAsync(targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);

            await _service.ApplyStrikeAsync(targetId, new StrikeLevelUpdateRequest { Level = 1 });

            _profileRepoMock.Verify(x => x.UpdateStrikeAsync(targetId, 1, "restricted", It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
            _mailMock.Verify(x => x.SendStrikeWarningEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<DateTime>()), Times.Once);
        }
    }
}

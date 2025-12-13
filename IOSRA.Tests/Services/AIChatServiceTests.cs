using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.AIChat;
using Contract.DTOs.Response.AIChat;
using Contract.DTOs.Response.Subscription;
using FluentAssertions;
using Moq;
using Repository.DataModels;
using Repository.Interfaces;
using Service.Interfaces;
using Service.Services;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class AIChatServiceTests
    {
        private readonly Mock<IOpenAiChatService> _aiMock;
        private readonly Mock<IAIChatRepository> _repoMock;
        private readonly Mock<ISubscriptionService> _subMock;
        private readonly AIChatService _service;
        private readonly Guid _userId = Guid.NewGuid();

        public AIChatServiceTests()
        {
            _aiMock = new Mock<IOpenAiChatService>();
            _repoMock = new Mock<IAIChatRepository>();
            _subMock = new Mock<ISubscriptionService>();
            _service = new AIChatService(_repoMock.Object, _aiMock.Object, _subMock.Object);
        }

        [Fact]
        public async Task SendAsync_Should_Return_Response()
        {
            var req = new AiChatSendRequest { Message = "Hello" };
            var aiResponse = "Hi there";
            
            _subMock.Setup(x => x.GetStatusAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubscriptionStatusResponse { HasActiveSubscription = true, PlanCode = "premium_month" });

            _aiMock.Setup(x => x.ExtractKeywordsAsync(req.Message, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            _repoMock.Setup(x => x.SearchContentAsync(It.IsAny<List<string>>(), 3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<string>());

            _repoMock.Setup(x => x.GetHistoryAsync(_userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AiChatStoredMessage>());

            _aiMock.Setup(x => x.ChatAsync(It.IsAny<List<AiChatPromptMessage>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(aiResponse);

            _repoMock.Setup(x => x.AppendAsync(_userId, It.IsAny<IReadOnlyList<AiChatStoredMessage>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repoMock.Setup(x => x.TrimAsync(_userId, 40, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var result = await _service.SendAsync(_userId, req);

            result.Messages.Should().HaveCount(2); // User + Assistant
        }
    }
}

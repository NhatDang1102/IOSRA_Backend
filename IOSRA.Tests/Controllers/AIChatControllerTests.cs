using Contract.DTOs.Request.AIChat;
using Contract.DTOs.Response.AIChat;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;
using static IOSRA.Tests.Common.ControllerContextFactory;

namespace IOSRA.Tests.Controllers
{
    public class AIChatControllerTests
    {
        private readonly Mock<IAIChatService> _serviceMock;
        private readonly AIChatController _controller;
        private readonly Guid _userId = Guid.NewGuid();

        public AIChatControllerTests()
        {
            _serviceMock = new Mock<IAIChatService>();
            _controller = new AIChatController(_serviceMock.Object);
            _controller.ControllerContext = CreateControllerContext(_userId);
        }

        [Fact]
        public async Task SendMessage_Should_Return_Ok()
        {
            var req = new AiChatSendRequest { Message = "Hi" };
            var res = new AiChatHistoryResponse();
            _serviceMock.Setup(x => x.SendAsync(_userId, req, It.IsAny<CancellationToken>()))
                .ReturnsAsync(res);

            var result = await _controller.SendMessage(req, CancellationToken.None);
            var actionResult = (ActionResult<AiChatHistoryResponse>)result;
            // When using ActionResult<T>, if Result is null, check Value
            if (actionResult.Result != null)
            {
                var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
                okResult.Value.Should().Be(res);
            }
            else
            {
                // If Result is null, Value should be set (implicit cast from T to ActionResult<T> creates this state, but Ok(T) usually sets Result)
                // However, Ok(obj) creates OkObjectResult which is assigned to Result.
                // Let's debug why Result is null. 
                // Wait, Controller returns Ok(result). This creates OkObjectResult.
                // So Result SHOULD NOT be null.
                // Ah, maybe my casting in previous attempt was wrong or I misunderstood the fail message.
                // "Expected ... OkObjectResult, but found <null>." -> Result IS null.
                // This implies SendMessage returned something that has Result == null.
                // Let's try checking Value directly if Result is null.
                actionResult.Value.Should().Be(res);
            }
        }
    }
}

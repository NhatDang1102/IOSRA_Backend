using FluentAssertions;
using Main.Controllers;
using Main.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Contract.DTOs.Request.Story;
using Contract.DTOs.Respond.Story;

namespace Main.Tests.Controllers
{
    /// <summary>
    /// Kiểm thử StoryModerationController (CMOD/Admin):
    /// - GET  /api/moderation/stories/pending        -> Ok + queue items
    /// - POST /api/moderation/stories/{id}/decision  -> Ok + message (approve/reject)
    /// </summary>
    public class StoryModerationControllerTests
    {
        private static StoryModerationController Create(out Mock<IStoryModerationService> mock)
        {
            mock = new Mock<IStoryModerationService>(MockBehavior.Strict);
            return new StoryModerationController(mock.Object);
        }

        [Fact]
        public async Task ListPending_ShouldReturnOk_WithItems()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(1000);

            var queue = new List<StoryModerationQueueItem> { new() { StoryId = 1, Title = "A" } };
            svc.Setup(s => s.ListPendingAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(queue);

            // Act
            var result = await controller.ListPending(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeSameAs(queue);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Decide_WhenApprove_ShouldReturnOk_WithApprovedMessage()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(77); // moderator account id

            var req = new StoryModerationDecisionRequest { Approve = true, ModeratorNote = "ok" };
            svc.Setup(s => s.ModerateAsync(77UL, 321UL, req, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            // Act
            var result = await controller.Decide(321UL, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Story approved." });
            svc.VerifyAll();
        }

        [Fact]
        public async Task Decide_WhenReject_ShouldReturnOk_WithRejectedMessage()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(77);

            var req = new StoryModerationDecisionRequest { Approve = false, ModeratorNote = "bad" };
            svc.Setup(s => s.ModerateAsync(77UL, 321UL, req, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            // Act
            var result = await controller.Decide(321UL, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Story rejected." });
            svc.VerifyAll();
        }
    }
}

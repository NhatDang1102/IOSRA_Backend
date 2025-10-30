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
    /// Kiểm thử StoryController (Author/Admin):
    /// - GET    /api/story                    -> Ok + List<StoryListItemResponse>
    /// - GET    /api/story/{storyId}          -> Ok + StoryResponse
    /// - POST   /api/story (multipart/form)   -> Ok + StoryResponse
    /// - POST   /api/story/{id}/submit        -> Ok + StoryResponse
    /// </summary>
    public class StoryControllerTests
    {
        private static StoryController Create(out Mock<IStoryService> mock)
        {
            mock = new Mock<IStoryService>(MockBehavior.Strict);
            return new StoryController(mock.Object);
        }

        [Fact]
        public async Task List_ShouldReturnOk_WithStories()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(10);

            var list = new List<StoryListItemResponse> { new() { StoryId = 1, Title = "A", Status = "draft" } };
            svc.Setup(s => s.ListAsync(10UL, It.IsAny<CancellationToken>()))
               .ReturnsAsync(list);

            // Act
            var result = await controller.List(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeSameAs(list);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Get_ShouldReturnOk_WithStory()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(11);

            var story = new StoryResponse { StoryId = 123, Title = "S", Status = "draft" };
            svc.Setup(s => s.GetAsync(11UL, 123UL, It.IsAny<CancellationToken>()))
               .ReturnsAsync(story);

            // Act
            var result = await controller.Get(123UL, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(story);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Create_ShouldReturnOk_WithCreatedStory()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(12);

            var req = new StoryCreateRequest
            {
                Title = "My story",
                TagIds = new List<uint> { 1 },
                CoverMode = "upload"
                // CoverFile có thể null trong unit test vì ta mock service; binder/validation nằm ngoài scope
            };
            var created = new StoryResponse { StoryId = 9, Title = "My story", Status = "draft" };

            svc.Setup(s => s.CreateAsync(12UL, req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(created);

            // Act
            var result = await controller.Create(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(created);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Submit_ShouldReturnOk_WithStory()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(13);

            var req = new StorySubmitRequest { Notes = "please review" };
            var updated = new StoryResponse { StoryId = 200, Title = "S2", Status = "pending" };

            svc.Setup(s => s.SubmitForReviewAsync(13UL, 200UL, req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(updated);

            // Act
            var result = await controller.Submit(200UL, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(updated);
            svc.VerifyAll();
        }
    }
}

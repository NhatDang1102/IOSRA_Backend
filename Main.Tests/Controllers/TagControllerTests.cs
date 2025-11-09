using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Contract.DTOs.Request.Tag;
using Contract.DTOs.Respond.Tag;

namespace Main.Tests.Controllers
{
    /// <summary>
    /// Kiểm thử TagController:
    /// - GET    /api/tag          -> Ok + tất cả tag (AllowAnonymous)
    /// - POST   /api/tag          -> Ok + TagResponse (CMOD/Admin)
    /// - PUT    /api/tag/{id}     -> Ok + TagResponse (CMOD/Admin)
    /// - DELETE /api/tag/{id}     -> NoContent         (CMOD/Admin)
    /// </summary>
    public class TagControllerTests
    {
        private static TagController Create(out Mock<ITagService> mock)
        {
            mock = new Mock<ITagService>(MockBehavior.Strict);
            return new TagController(mock.Object);
        }

        [Fact]
        public async Task GetAll_ShouldReturnOk_WithTags()
        {
            // Arrange – endpoint này AllowAnonymous, không cần user
            var controller = Create(out var svc);
            var tags = new List<TagResponse> { new() { TagId = 1, Name = "Fantasy" } };

            svc.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(tags);

            // Act
            var result = await controller.GetAll(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeSameAs(tags);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Create_ShouldReturnOk_WithTag()
        {
            // Arrange
            var controller = Create(out var svc);
            var req = new TagCreateRequest { Name = "Fantasy" };
            var tag = new TagResponse { TagId = 9, Name = "Fantasy" };

            svc.Setup(s => s.CreateAsync(req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(tag);

            // Act
            var result = await controller.Create(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(tag);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Update_ShouldReturnOk_WithTag()
        {
            // Arrange
            var controller = Create(out var svc);
            var req = new TagUpdateRequest { Name = "Romance" };
            var tag = new TagResponse { TagId = 5, Name = "Romance" };

            svc.Setup(s => s.UpdateAsync(5U, req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(tag);

            // Act
            var result = await controller.Update(5U, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(tag);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Delete_ShouldReturnNoContent()
        {
            // Arrange
            var controller = Create(out var svc);

            svc.Setup(s => s.DeleteAsync(7U, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            // Act
            var result = await controller.Delete(7U, CancellationToken.None);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            svc.VerifyAll();
        }
    }
}

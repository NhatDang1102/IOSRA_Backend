using FluentAssertions;
using Main.Controllers;
using Main.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Contract.DTOs.Request.Author;
using Contract.DTOs.Respond.Author;
using Contract.DTOs.Respond.OperationMod;

namespace Main.Tests.Controllers
{
    /// <summary>
    /// Kiểm thử AuthorUpgradeController:
    /// - POST /api/authorupgrade/request (Submit)         -> Ok + AuthorUpgradeResponse
    /// - GET  /api/authorupgrade/my-requests (MyRequests) -> Ok + List<OpRequestItemResponse>
    /// - Kiểm tra route ở mức controller [Route("api/[controller]")]
    /// </summary>
    public class AuthorUpgradeControllerTests
    {
        private static AuthorUpgradeController Create(out Mock<IAuthorUpgradeService> mock)
        {
            mock = new Mock<IAuthorUpgradeService>(MockBehavior.Strict);
            return new AuthorUpgradeController(mock.Object);
        }

        [Fact]
        public async Task Submit_ShouldReturnOk_WithAuthorUpgradeResponse()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(100);

            var request = new SubmitAuthorUpgradeRequest { Commitment = "I will follow rules" };
            var expected = new AuthorUpgradeResponse { RequestId = 1, Status = "pending" };

            svc.Setup(s => s.SubmitAsync(100UL, request, It.IsAny<CancellationToken>()))
               .ReturnsAsync(expected);

            // Act
            var result = await controller.Submit(request, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);
            svc.VerifyAll();
        }

        [Fact]
        public async Task MyRequests_ShouldReturnOk_WithItems()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(55);

            var items = new List<OpRequestItemResponse> { new() { RequestId = 9, Status = "pending" } };
            svc.Setup(s => s.ListMyRequestsAsync(55UL, It.IsAny<CancellationToken>()))
               .ReturnsAsync(items);

            // Act
            var result = await controller.MyRequests(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeSameAs(items);
            svc.VerifyAll();
        }

        [Fact]
        public void Controller_ShouldHaveRoute_ApiController()
        {
            var route = typeof(AuthorUpgradeController)
                .GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>()
                .FirstOrDefault();

            route.Should().NotBeNull();
            route!.Template.Should().Be("api/[controller]");
        }
    }
}

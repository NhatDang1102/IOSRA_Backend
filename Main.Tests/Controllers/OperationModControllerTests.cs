using FluentAssertions;
using Main.Controllers;
using Main.Tests.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Contract.DTOs.Respond.OperationMod;
using Contract.DTOs.Request.OperationMod;

namespace Main.Tests.Controllers
{
    /// <summary>
    /// Kiểm thử OperationModController:
    /// - GET  /api/operationmod/requests?status=... -> Ok + List<OpRequestItemResponse>
    /// - POST /api/operationmod/requests/{id}/approve -> Ok + { message = "Approved" }
    /// - POST /api/operationmod/requests/{id}/reject  -> Ok + { message = "Rejected" }
    /// - Kiểm tra [Authorize(Roles=...)] & [Route("api/[controller]")]
    /// </summary>
    public class OperationModControllerTests
    {
        private static OperationModController Create(out Mock<IOperationModService> mock)
        {
            mock = new Mock<IOperationModService>(MockBehavior.Strict);
            return new OperationModController(mock.Object);
        }

        [Fact]
        public async Task List_ShouldReturnOk_WithItems()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(999); // omod/admin id

            var items = new List<OpRequestItemResponse> { new() { RequestId = 7, Status = "pending" } };
            svc.Setup(s => s.ListAsync("pending", It.IsAny<CancellationToken>()))
               .ReturnsAsync(items);

            // Act
            var result = await controller.List("pending", CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeSameAs(items);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Approve_ShouldReturnOk_WithApprovedMessage()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(7); // omod id

            svc.Setup(s => s.ApproveAsync(123UL, 7UL, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            // Act
            var result = await controller.Approve(123UL, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Approved" });
            svc.VerifyAll();
        }

        [Fact]
        public async Task Reject_ShouldReturnOk_WithRejectedMessage()
        {
            // Arrange
            var controller = Create(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(8); // omod id

            var req = new RejectAuthorUpgradeRequest { Reason = "not enough info" };
            svc.Setup(s => s.RejectAsync(123UL, 8UL, req, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            // Act
            var result = await controller.Reject(123UL, req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Rejected" });
            svc.VerifyAll();
        }

        [Fact]
        public void Controller_ShouldHaveAuthorizeRoles_AndRoute()
        {
            var authorize = typeof(OperationModController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), true)
                .Cast<AuthorizeAttribute>()
                .FirstOrDefault();
            authorize.Should().NotBeNull();
            authorize!.Roles.Should().Contain("omod");

            var route = typeof(OperationModController)
                .GetCustomAttributes(typeof(RouteAttribute), true)
                .Cast<RouteAttribute>()
                .FirstOrDefault();
            route.Should().NotBeNull();
            route!.Template.Should().Be("api/[controller]");
        }
    }
}

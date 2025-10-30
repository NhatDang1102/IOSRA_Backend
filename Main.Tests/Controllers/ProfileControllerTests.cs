using Contract.DTOs.Request.Profile;
using Contract.DTOs.Respond.Profile;
using FluentAssertions;
using Main.Controllers;
using Main.Tests.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;

namespace Main.Tests.Controllers
{
    /// <summary>
    /// Kiểm thử ProfileController (kế thừa AppControllerBase):
    /// - GET    /api/profile                    -> Ok + ProfileResponse
    /// - Thiếu claim 'sub'                      -> UnauthorizedAccessException
    /// - PUT    /api/profile                    -> Ok + ProfileResponse
    /// - POST   /api/profile/avatar (form-data) -> Ok + { avatarUrl }
    /// - POST   /api/profile/email/otp          -> Ok + message chuẩn
    /// - POST   /api/profile/email/verify       -> Ok + message chuẩn
    /// </summary>
    public class ProfileControllerTests
    {
        private static ProfileController CreateController(out Mock<IProfileService> profileServiceMock)
        {
            profileServiceMock = new Mock<IProfileService>(MockBehavior.Strict);
            return new ProfileController(profileServiceMock.Object);
        }

        [Fact]
        public async Task Get_ShouldReturnOk_WithProfileResponse()
        {
            // Arrange: user có claim "sub" = 123
            var controller = CreateController(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(123);

            var expected = new ProfileResponse
            {
                AccountId = 123,
                Username = "u123",
                Email = "u123@mail.com"
            };

            svc.Setup(s => s.GetAsync(123UL, It.IsAny<CancellationToken>()))
               .ReturnsAsync(expected);

            // Act
            var result = await controller.Get(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);
            svc.VerifyAll();
        }

        [Fact]
        public async Task Get_WhenMissingSubClaim_ShouldThrowUnauthorizedAccess()
        {
            // Arrange: user trống (không claim)
            var controller = CreateController(out _);
            controller.ControllerContext = ControllerContextFactory.EmptyUser();

            // Act
            var act = async () => await controller.Get(CancellationToken.None);

            // Assert: AppControllerBase sẽ truy cập AccountId và ném lỗi khi thiếu claim
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task Update_ShouldReturnOk_WithUpdatedProfile()
        {
            // Arrange
            var controller = CreateController(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(7);

            var req = new ProfileUpdateRequest { Bio = "bio", Gender = "M" };
            var updated = new ProfileResponse
            {
                AccountId = 7,
                Username = "user7",
                Email = "u7@mail.com",
                Bio = "bio",
                Gender = "M"
            };

            svc.Setup(s => s.UpdateAsync(7UL, req, It.IsAny<CancellationToken>()))
               .ReturnsAsync(updated);

            // Act
            var result = await controller.Update(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(updated);
            svc.VerifyAll();
        }

        [Fact]
        public async Task UpdateAvatar_ShouldReturnOk_WithAvatarUrl()
        {
            // Arrange: tạo file giả IFormFile
            var controller = CreateController(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(7);

            await using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
            IFormFile fakeFile = new FormFile(ms, 0, ms.Length, "file", "avatar.png");

            var req = new AvatarUploadRequest { File = fakeFile };
            var expectedUrl = "https://cdn.example/avatars/u7.png";

            svc.Setup(s => s.UpdateAvatarAsync(7UL, fakeFile, It.IsAny<CancellationToken>()))
               .ReturnsAsync(expectedUrl);

            // Act
            var result = await controller.UpdateAvatar(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { avatarUrl = expectedUrl });
            svc.VerifyAll();
        }

        [Fact]
        public async Task SendChangeEmailOtp_ShouldReturnOk_WithMessage()
        {
            // Arrange
            var controller = CreateController(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(9);

            var req = new ChangeEmailRequest { NewEmail = "new@mail.com" };

            svc.Setup(s => s.SendChangeEmailOtpAsync(9UL, req, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            // Act
            var result = await controller.SendChangeEmailOtp(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Nếu email hợp lệ, OTP đã được gửi." });
            svc.VerifyAll();
        }

        [Fact]
        public async Task VerifyChangeEmail_ShouldReturnOk_WithMessage()
        {
            // Arrange
            var controller = CreateController(out var svc);
            controller.ControllerContext = ControllerContextFactory.WithUser(9);

            var req = new VerifyChangeEmailRequest { Otp = "123456" };

            svc.Setup(s => s.VerifyChangeEmailAsync(9UL, req, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

            // Act
            var result = await controller.VerifyChangeEmail(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Đổi email thành công." });
            svc.VerifyAll();
        }
    }
}

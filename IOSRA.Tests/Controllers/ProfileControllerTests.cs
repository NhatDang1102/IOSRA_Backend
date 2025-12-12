using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Profile;
using Contract.DTOs.Response.Profile;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class ProfileControllerTests
    {
        private readonly Mock<IProfileService> _profile;
        private readonly ProfileController _controller;
        private readonly Guid _accountId = Guid.NewGuid();

        public ProfileControllerTests()
        {
            _profile = new Mock<IProfileService>(MockBehavior.Strict);
            _controller = new ProfileController(_profile.Object);

            SetUserWithAccountId(_accountId);
        }

        private void SetUserWithAccountId(Guid accountId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, accountId.ToString())
            }, "TestAuth");

            var user = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user
                }
            };
        }

        [Fact]
        public async Task Get_Should_Return_Profile_From_Service()
        {
            // Arrange
            var expected = new ProfileResponse
            {
                AccountId = _accountId,
                Username = "testuser",
                Email = "test@example.com",
                Bio = "bio",
                Gender = "unspecified",
                IsAuthor = false
            };

            _profile.Setup(p => p.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected)
                    .Verifiable();

            // Act
            var result = await _controller.Get(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _profile.Verify(p => p.GetAsync(_accountId, It.IsAny<CancellationToken>()), Times.Once);
            _profile.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GetWallet_Should_Return_ProfileWalletResponse_From_Service()
        {
            // Arrange
            var expected = new ProfileWalletResponse
            {
                DiaBalance = 100,
                IsAuthor = true
            };

            _profile.Setup(p => p.GetWalletAsync(_accountId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected)
                    .Verifiable();

            // Act
            var result = await _controller.GetWallet(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _profile.Verify(p => p.GetWalletAsync(_accountId, It.IsAny<CancellationToken>()), Times.Once);
            _profile.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Update_Should_Call_Service_And_Return_Updated_Profile()
        {
            // Arrange
            var req = new ProfileUpdateRequest
            {
                Bio = "new bio",
                Gender = "M",
                Birthday = DateOnly.FromDateTime(DateTime.UtcNow.Date)
            };

            var expected = new ProfileResponse
            {
                AccountId = _accountId,
                Username = "testuser",
                Email = "test@example.com",
                Bio = req.Bio,
                Gender = req.Gender ?? "unspecified"
            };

            _profile.Setup(p => p.UpdateAsync(_accountId, req, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected)
                    .Verifiable();

            // Act
            var result = await _controller.Update(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _profile.Verify(p => p.UpdateAsync(_accountId, req, It.IsAny<CancellationToken>()), Times.Once);
            _profile.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UpdateAvatar_Should_Call_Service_And_Return_Url()
        {
            // Arrange
            var file = new Mock<IFormFile>().Object;
            var req = new AvatarUploadRequest
            {
                File = file
            };

            var expectedUrl = "https://example.com/avatar.jpg";

            _profile.Setup(p => p.UpdateAvatarAsync(_accountId, req.File, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expectedUrl)
                    .Verifiable();

            // Act
            var result = await _controller.UpdateAvatar(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { avatarUrl = expectedUrl });

            _profile.Verify(p => p.UpdateAvatarAsync(_accountId, req.File, It.IsAny<CancellationToken>()), Times.Once);
            _profile.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SendChangeEmailOtp_Should_Call_Service_And_Return_Ok()
        {
            // Arrange
            var req = new ChangeEmailRequest
            {
                NewEmail = "new@example.com"
            };

            _profile.Setup(p => p.SendChangeEmailOtpAsync(_accountId, req, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

            // Act
            var result = await _controller.SendChangeEmailOtp(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            _profile.Verify(p => p.SendChangeEmailOtpAsync(_accountId, req, It.IsAny<CancellationToken>()), Times.Once);
            _profile.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task VerifyChangeEmail_Should_Call_Service_And_Return_Ok()
        {
            // Arrange
            var req = new VerifyChangeEmailRequest
            {
                Otp = "123456"
            };

            _profile.Setup(p => p.VerifyChangeEmailAsync(_accountId, req, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask)
                    .Verifiable();

            // Act
            var result = await _controller.VerifyChangeEmail(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            _profile.Verify(p => p.VerifyChangeEmailAsync(_accountId, req, It.IsAny<CancellationToken>()), Times.Once);
            _profile.VerifyNoOtherCalls();
        }
    }
}

using Contract.DTOs.Request.Auth;
using Contract.DTOs.Respond.Auth;
using FluentAssertions;
using Main.Controllers;
using Main.Tests.Common;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using System.IdentityModel.Tokens.Jwt;

namespace Main.Tests.Controllers
{
    /// <summary>
    /// Kiểm thử AuthController:
    /// - POST /api/auth/register             → Gửi OTP, Ok + message
    /// - POST /api/auth/verify               → Xác minh đăng ký, Ok + LoginResponse
    /// - POST /api/auth/login                → Đăng nhập thường, Ok + LoginResponse
    /// - POST /api/auth/google               → Đăng nhập Google, Ok + LoginResponse
    /// - POST /api/auth/google/complete      → Hoàn tất đăng ký Google, Ok + LoginResponse
    /// - POST /api/auth/logout               → Blacklist token với jti/exp hợp lệ, Ok + message
    /// - POST /api/auth/forgot-pass          → Gửi OTP quên mật khẩu, Ok + message
    /// - POST /api/auth/forgot-pass/verify   → Xác minh OTP, đổi mật khẩu, Ok + message
    /// </summary>
    public class AuthControllerTests
    {
        private static AuthController CreateController(
            out Mock<IAuthService> authMock,
            out Mock<IJwtBlacklistService> blacklistMock)
        {
            authMock = new Mock<IAuthService>(MockBehavior.Strict);
            blacklistMock = new Mock<IJwtBlacklistService>(MockBehavior.Strict);
            return new AuthController(authMock.Object, blacklistMock.Object);
        }

        [Fact]
        public async Task Register_ShouldReturnOk_WithMessage()
        {
            // Arrange
            var controller = CreateController(out var authMock, out _);
            var req = new RegisterRequest
            {
                Username = "abc",
                Email = "a@b.com",
                Password = "123abc",
                ConfirmPassword = "123abc"
            };

            authMock.Setup(a => a.SendRegisterOtpAsync(req, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            // Act
            var result = await controller.Register(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "OTP sent. Please check your email." });
            authMock.VerifyAll();
        }

        [Fact]
        public async Task Verify_ShouldReturnOk_WithLoginResponse()
        {
            // Arrange
            var controller = CreateController(out var authMock, out _);
            var req = new VerifyOtpRequest { Email = "a@b.com", Otp = "123456" };
            var expected = new LoginResponse
            {
                AccountId = 1,
                Username = "user1",
                Email = "a@b.com",
                Token = "jwt_token_123",
                Roles = new() { "reader" }
            };

            authMock.Setup(a => a.VerifyRegisterAsync(req, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);

            // Act
            var result = await controller.Verify(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(expected);
            authMock.VerifyAll();
        }

        [Fact]
        public async Task Login_ShouldReturnOk_WithLoginResponse()
        {
            // Arrange
            var controller = CreateController(out var authMock, out _);
            var req = new LoginRequest { Identifier = "user", Password = "pass123" };
            var expected = new LoginResponse { AccountId = 2, Username = "user", Email = "u@e.com", Token = "jwt", Roles = new() { "reader" } };

            authMock.Setup(a => a.LoginAsync(req, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);

            // Act
            var result = await controller.Login(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(expected);
            authMock.VerifyAll();
        }

        [Fact]
        public async Task GoogleLogin_ShouldReturnOk_WithLoginResponse()
        {
            // Arrange
            var controller = CreateController(out var authMock, out _);
            var req = new GoogleLoginRequest { IdToken = "token_abc" };
            var expected = new LoginResponse { AccountId = 3, Username = "gguser", Email = "g@mail.com", Token = "jwt_gg" };

            authMock.Setup(a => a.LoginWithGoogleAsync(req, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);

            // Act
            var result = await controller.GoogleLogin(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(expected);
            authMock.VerifyAll();
        }

        [Fact]
        public async Task CompleteGoogleRegister_ShouldReturnOk_WithLoginResponse()
        {
            // Arrange
            var controller = CreateController(out var authMock, out _);
            var req = new CompleteGoogleRegisterRequest
            {
                IdToken = "token123",
                Username = "u1",
                Password = "p12345",
                ConfirmPassword = "p12345"
            };
            var expected = new LoginResponse { AccountId = 4, Username = "u1", Email = "e@e.com", Token = "jwt_done" };

            authMock.Setup(a => a.CompleteGoogleRegisterAsync(req, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(expected);

            // Act
            var result = await controller.CompleteGoogleRegister(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(expected);
            authMock.VerifyAll();
        }

        [Fact]
        public async Task Logout_ShouldReturnOk_WhenValidJtiAndExp()
        {
            // Arrange
            var controller = CreateController(out _, out var blacklistMock);

            // gắn user có claim jti/exp (sub không bắt buộc cho action này)
            var exp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
            controller.ControllerContext = ControllerContextFactory.WithUser(
                sub: null,
                extra: new Dictionary<string, string>
                {
                    { JwtRegisteredClaimNames.Jti, "jti_123" },
                    { "exp", exp.ToString() }
                });

            blacklistMock.Setup(b => b.BlacklistAsync("jti_123",
                It.Is<DateTimeOffset>(d => d.ToUnixTimeSeconds() == exp),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await controller.Logout(CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Đăng xuất thành công." });
            blacklistMock.VerifyAll();
        }

        [Fact]
        public async Task Logout_ShouldReturnBadRequest_WhenMissingClaims()
        {
            // Arrange
            var controller = CreateController(out _, out _);
            controller.ControllerContext = ControllerContextFactory.EmptyUser();

            // Act
            var result = await controller.Logout(CancellationToken.None);

            // Assert
            var bad = result as BadRequestObjectResult;
            bad.Should().NotBeNull();
            bad!.Value.Should().BeEquivalentTo(new
            {
                error = new { code = "InvalidToken", message = "Không tìm thấy thông tin JTI/EXP trong token." }
            }, opts => opts.ComparingByMembers<object>());
        }

        [Fact]
        public async Task ForgotPassword_ShouldReturnOk_WithMessage()
        {
            // Arrange
            var controller = CreateController(out var authMock, out _);
            var req = new ForgotPasswordRequest { Email = "x@y.com" };

            authMock.Setup(a => a.SendForgotOtpAsync(req, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            // Act
            var result = await controller.ForgotPassword(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Nếu email tồn tại, OTP đã được gửi." });
            authMock.VerifyAll();
        }

        [Fact]
        public async Task VerifyForgotPassword_ShouldReturnOk_WithMessage()
        {
            // Arrange
            var controller = CreateController(out var authMock, out _);
            var req = new VerifyForgotPasswordRequest
            {
                Email = "u@mail.com",
                Otp = "111222",
                NewPassword = "a12345",
                ConfirmNewPassword = "a12345"
            };

            authMock.Setup(a => a.VerifyForgotAsync(req, It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            // Act
            var result = await controller.VerifyForgotPassword(req, CancellationToken.None);

            // Assert
            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().BeEquivalentTo(new { message = "Đổi mật khẩu thành công." });
            authMock.VerifyAll();
        }
    }
}

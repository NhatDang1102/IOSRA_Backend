using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Response.Auth;
using FluentAssertions;
using Main.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Service.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace IOSRA.Tests.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<IAuthService> _auth;
        private readonly Mock<IJwtBlacklistService> _blacklist;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _auth = new Mock<IAuthService>(MockBehavior.Strict);
            _blacklist = new Mock<IJwtBlacklistService>(MockBehavior.Strict);
            _controller = new AuthController(_auth.Object, _blacklist.Object);
        }

        private void SetUser(params Claim[] claims)
        {
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task Register_Should_Call_Service_And_Return_Ok()
        {
            var req = new RegisterRequest
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "Pass123",
                ConfirmPassword = "Pass123"
            };

            _auth.Setup(a => a.SendRegisterOtpAsync(req, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Verifiable();

            var result = await _controller.Register(req, CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            _auth.Verify(a => a.SendRegisterOtpAsync(req, It.IsAny<CancellationToken>()), Times.Once);
            _auth.VerifyNoOtherCalls();
            _blacklist.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Verify_Should_Return_LoginResponse()
        {
            var req = new VerifyOtpRequest { Email = "test@example.com", Otp = "123456" };

            var expected = new LoginResponse
            {
                AccountId = Guid.NewGuid(),
                Username = "testuser",
                Token = "jwt-token",
                Roles = new() { "User" }
            };

            _auth.Setup(a => a.VerifyRegisterAsync(req, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(expected)
                 .Verifiable();

            var result = await _controller.Verify(req, CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _auth.Verify(a => a.VerifyRegisterAsync(req, It.IsAny<CancellationToken>()), Times.Once);
            _auth.VerifyNoOtherCalls();
            _blacklist.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Login_Should_Return_LoginResponse()
        {
            var req = new LoginRequest
            {
                Identifier = "test@example.com",
                Password = "Pass123"
            };

            var expected = new LoginResponse
            {
                AccountId = Guid.NewGuid(),
                Username = "testuser",
                Token = "jwt-token",
                Roles = new() { "User" }
            };

            _auth.Setup(a => a.LoginAsync(req, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(expected)
                 .Verifiable();

            var result = await _controller.Login(req, CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _auth.Verify(a => a.LoginAsync(req, It.IsAny<CancellationToken>()), Times.Once);
            _auth.VerifyNoOtherCalls();
            _blacklist.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task GoogleLogin_Should_Return_LoginResponse()
        {
            var req = new GoogleLoginRequest { IdToken = "google-token" };

            var expected = new LoginResponse
            {
                AccountId = Guid.NewGuid(),
                Username = "testuser",
                Token = "jwt-token",
                Roles = new() { "User" }
            };

            _auth.Setup(a => a.LoginWithGoogleAsync(req, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(expected)
                 .Verifiable();

            var result = await _controller.GoogleLogin(req, CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _auth.Verify(a => a.LoginWithGoogleAsync(req, It.IsAny<CancellationToken>()), Times.Once);
            _auth.VerifyNoOtherCalls();
            _blacklist.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task CompleteGoogleRegister_Should_Return_LoginResponse()
        {
            var req = new CompleteGoogleRegisterRequest
            {
                IdToken = "google-token",
                Username = "testuser"
            };

            var expected = new LoginResponse
            {
                AccountId = Guid.NewGuid(),
                Username = "testuser",
                Token = "jwt-token",
                Roles = new() { "User" }
            };

            _auth.Setup(a => a.CompleteGoogleRegisterAsync(req, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(expected)
                 .Verifiable();

            var result = await _controller.CompleteGoogleRegister(req, CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();
            ok!.Value.Should().Be(expected);

            _auth.Verify(a => a.CompleteGoogleRegisterAsync(req, It.IsAny<CancellationToken>()), Times.Once);
            _auth.VerifyNoOtherCalls();
            _blacklist.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ForgotPassword_Should_Return_Ok()
        {
            var req = new ForgotPasswordRequest { Email = "test@example.com" };

            _auth.Setup(a => a.SendForgotOtpAsync(req, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Verifiable();

            var result = await _controller.ForgotPassword(req, CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            _auth.Verify(a => a.SendForgotOtpAsync(req, It.IsAny<CancellationToken>()), Times.Once);
            _auth.VerifyNoOtherCalls();
            _blacklist.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task VerifyForgotPassword_Should_Return_Ok()
        {
            var req = new VerifyForgotPasswordRequest
            {
                Email = "t@example.com",
                Otp = "1234",
                NewPassword = "Pass123",
                ConfirmNewPassword = "Pass123"
            };

            _auth.Setup(a => a.VerifyForgotAsync(req, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask)
                 .Verifiable();

            var result = await _controller.VerifyForgotPassword(req, CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            _auth.Verify(a => a.VerifyForgotAsync(req, It.IsAny<CancellationToken>()), Times.Once);
            _auth.VerifyNoOtherCalls();
            _blacklist.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Logout_Should_Add_JWT_To_Blacklist()
        {
            var jti = "jti-123";
            var exp = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();

            SetUser(
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim("exp", exp.ToString())
            );

            _blacklist.Setup(b => b.BlacklistAsync(
                    jti,
                    It.Is<DateTimeOffset>(d => d.ToUnixTimeSeconds() == exp),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            var result = await _controller.Logout(CancellationToken.None);

            var ok = result as OkObjectResult;
            ok.Should().NotBeNull();

            _blacklist.Verify(b => b.BlacklistAsync(
                jti,
                It.Is<DateTimeOffset>(d => d.ToUnixTimeSeconds() == exp),
                It.IsAny<CancellationToken>()),
                Times.Once);

            _blacklist.VerifyNoOtherCalls();
            _auth.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Logout_Should_Fail_When_Missing_Claims()
        {
            SetUser(); // no claims

            var result = await _controller.Logout(CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();

            _blacklist.VerifyNoOtherCalls();
            _auth.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Logout_Should_Fail_When_Exp_Invalid()
        {
            SetUser(
                new Claim(JwtRegisteredClaimNames.Jti, "abc"),
                new Claim("exp", "not-number")
            );

            var result = await _controller.Logout(CancellationToken.None);

            result.Should().BeOfType<BadRequestObjectResult>();

            _blacklist.VerifyNoOtherCalls();
            _auth.VerifyNoOtherCalls();
        }
    }
}

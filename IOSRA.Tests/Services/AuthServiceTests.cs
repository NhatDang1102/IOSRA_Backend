using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BCrypt.Net;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Response.Auth;
using FluentAssertions;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Implementations;
using Service.Interfaces;
using Service.Models;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class AuthServiceTests
    {
        private readonly Mock<IAuthRepository> _repo;
        private readonly Mock<IJwtTokenFactory> _jwt;
        private readonly Mock<IOtpStore> _otpStore;
        private readonly Mock<IMailSender> _mail;
        private readonly Mock<IFirebaseAuthVerifier> _firebase;
        private readonly AuthService _svc;

        public AuthServiceTests()
        {
            _repo = new Mock<IAuthRepository>(MockBehavior.Strict);
            _jwt = new Mock<IJwtTokenFactory>(MockBehavior.Strict);
            _otpStore = new Mock<IOtpStore>(MockBehavior.Strict);
            _mail = new Mock<IMailSender>(MockBehavior.Strict);
            _firebase = new Mock<IFirebaseAuthVerifier>(MockBehavior.Strict);

            _svc = new AuthService(_repo.Object, _jwt.Object, _otpStore.Object, _mail.Object, _firebase.Object);
        }

        private static account MakeAccount(Guid id, string status = "unbanned") =>
            new account
            {
                account_id = id,
                username = "user01",
                email = "u@ex.com",
                password_hash = BCrypt.Net.BCrypt.HashPassword("123456"),
                status = status,
                strike = 0
            };

        #region SendRegisterOtpAsync

        // CASE: Request thiếu field -> 400
        [Fact]
        public async Task SendRegisterOtpAsync_Should_Throw_When_Request_Incomplete()
        {
            var req = new RegisterRequest { Username = "", Email = "a@ex.com", Password = "123456" };

            var act = () => _svc.SendRegisterOtpAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Chứa khoảng trống.*");

            _repo.VerifyNoOtherCalls();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: Username/email đã tồn tại -> 409
        [Fact]
        public async Task SendRegisterOtpAsync_Should_Throw_When_Account_Exists()
        {
            var req = new RegisterRequest { Username = "u1", Email = "a@ex.com", Password = "123456" };

            _repo.Setup(r => r.ExistsByUsernameOrEmailAsync(req.Username, req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

            var act = () => _svc.SendRegisterOtpAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                .WithMessage("*Email/Username đã tồn tại.*");

            _repo.VerifyAll();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: Bị rate-limit OTP -> 429
        [Fact]
        public async Task SendRegisterOtpAsync_Should_Throw_When_RateLimited()
        {
            var req = new RegisterRequest { Username = "u1", Email = "a@ex.com", Password = "123456" };

            _repo.Setup(r => r.ExistsByUsernameOrEmailAsync(req.Username, req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
            _otpStore.Setup(s => s.CanSendAsync(req.Email)).ReturnsAsync(false);

            var act = () => _svc.SendRegisterOtpAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
             .WithMessage("*OTP request quá số lần cho phép.*");

            _repo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: Happy path: lưu OTP + gửi mail
        [Fact]
        public async Task SendRegisterOtpAsync_Should_Save_Otp_And_Send_Email()
        {
            var req = new RegisterRequest { Username = "u1", Email = "a@ex.com", Password = "123456" };

            _repo.Setup(r => r.ExistsByUsernameOrEmailAsync(req.Username, req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);
            _otpStore.Setup(s => s.CanSendAsync(req.Email)).ReturnsAsync(true);

            _otpStore.Setup(s => s.SaveAsync(
                    req.Email,
                    It.IsAny<string>(),            // otp random
                    It.IsAny<string>(),            // bcrypt
                    req.Username))
                .Returns(Task.CompletedTask);

            _mail.Setup(m => m.SendOtpEmailAsync(req.Email, It.IsAny<string>()))
                 .Returns(Task.CompletedTask);

            await _svc.SendRegisterOtpAsync(req, CancellationToken.None);

            _repo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyAll();
        }

        #endregion

        #region VerifyRegisterAsync

        // CASE: Không tìm thấy entry OTP -> 400
        [Fact]
        public async Task VerifyRegisterAsync_Should_Throw_When_Otp_Entry_Missing()
        {
            var req = new VerifyOtpRequest { Email = "a@ex.com", Otp = "123456" };

            _otpStore.Setup(s => s.GetAsync(req.Email))
                     .ReturnsAsync((ValueTuple<string, string, string>?)null);

            var act = () => _svc.VerifyRegisterAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*OTP hết hạn hoặc không hợp lệ.*");

            _otpStore.VerifyAll();
            _repo.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
            _jwt.VerifyNoOtherCalls();
        }

        // CASE: OTP sai -> 400
        [Fact]
        public async Task VerifyRegisterAsync_Should_Throw_When_Otp_Not_Match()
        {
            var req = new VerifyOtpRequest { Email = "a@ex.com", Otp = "000000" };

            _otpStore.Setup(s => s.GetAsync(req.Email))
                     .ReturnsAsync(("123456", "hash", "u1"));

            var act = () => _svc.VerifyRegisterAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*OTP hết hạn hoặc không hợp lệ.*");

            _otpStore.VerifyAll();
            _repo.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
            _jwt.VerifyNoOtherCalls();
        }

        // CASE: Happy path: tạo account, reader, role, token
        [Fact]
        public async Task VerifyRegisterAsync_Should_Create_Account_And_Return_Token()
        {
            var req = new VerifyOtpRequest { Email = "a@ex.com", Otp = "123456" };
            var pwdHash = BCrypt.Net.BCrypt.HashPassword("123456");
            var username = "u1";

            _otpStore.Setup(s => s.GetAsync(req.Email))
                     .ReturnsAsync((req.Otp, pwdHash, username));

            var newId = Guid.NewGuid();
            _repo.Setup(r => r.AddAccountAsync(It.IsAny<account>(), It.IsAny<CancellationToken>()))
                 .Callback<account, CancellationToken>((a, _) => a.account_id = newId)
                 .ReturnsAsync((account a, CancellationToken _) => a);

            _repo.Setup(r => r.AddReaderAsync(It.Is<reader>(rd => rd.account_id == newId), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((reader rd, CancellationToken _) => rd);

            var roleId = Guid.NewGuid();
            _repo.Setup(r => r.GetRoleIdByCodeAsync("reader", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(roleId);
            _repo.Setup(r => r.AddAccountRoleAsync(newId, roleId, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

            _otpStore.Setup(s => s.DeleteAsync(req.Email))
                     .ReturnsAsync(true);

            _mail.Setup(m => m.SendWelcomeEmailAsync(req.Email, username))
                 .Returns(Task.CompletedTask);

            _jwt.Setup(j => j.CreateToken(It.Is<account>(a => a.account_id == newId), It.Is<IEnumerable<string>>(r => r.Contains("reader"))))
                .Returns(new JwtTokenResult
                {
                    Token = "jwt-token",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });


            var res = await _svc.VerifyRegisterAsync(req, CancellationToken.None);

            res.AccountId.Should().Be(newId);
            res.Username.Should().Be(username);
            res.Email.Should().Be(req.Email);
            res.Token.Should().Be("jwt-token");
            res.Roles.Should().ContainSingle("reader");

            _otpStore.VerifyAll();
            _repo.VerifyAll();
            _mail.VerifyAll();
            _jwt.VerifyAll();
        }

        #endregion

        #region LoginAsync

        // CASE: Account không tồn tại -> 401
        [Fact]
        public async Task LoginAsync_Should_Throw_When_Account_NotFound()
        {
            var req = new LoginRequest { Identifier = "u1", Password = "123456" };

            _repo.Setup(r => r.FindAccountByIdentifierAsync(req.Identifier, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((account?)null);

            var act = () => _svc.LoginAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Tài khoản không tồn tại.*");

            _repo.VerifyAll();
            _jwt.VerifyNoOtherCalls();
        }

        // CASE: Account bị banned -> 403
        [Fact]
        public async Task LoginAsync_Should_Throw_When_Account_Banned()
        {
            var req = new LoginRequest { Identifier = "u1", Password = "123456" };
            var acc = MakeAccount(Guid.NewGuid(), status: "banned");

            _repo.Setup(r => r.FindAccountByIdentifierAsync(req.Identifier, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);

            var act = () => _svc.LoginAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Tài khoản bị banned.*");

            _repo.VerifyAll();
        }

        // CASE: Sai password -> 401
        [Fact]
        public async Task LoginAsync_Should_Throw_When_Password_Invalid()
        {
            var req = new LoginRequest { Identifier = "u1", Password = "wrong" };
            var acc = MakeAccount(Guid.NewGuid());

            _repo.Setup(r => r.FindAccountByIdentifierAsync(req.Identifier, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);

            var act = () => _svc.LoginAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Sai pass.*");

            _repo.VerifyAll();
        }

        // CASE: Happy path: trả LoginResponse + token
        [Fact]
        public async Task LoginAsync_Should_Return_Token_On_Success()
        {
            var req = new LoginRequest { Identifier = "u1", Password = "123456" };
            var acc = MakeAccount(Guid.NewGuid());
            var roles = new List<string> { "reader", "author" };

            _repo.Setup(r => r.FindAccountByIdentifierAsync(req.Identifier, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);
            _repo.Setup(r => r.GetRoleCodesOfAccountAsync(acc.account_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(roles);

            _jwt.Setup(j => j.CreateToken(acc, roles))
                .Returns(new JwtTokenResult
                {
                    Token = "jwt-login",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });

            var res = await _svc.LoginAsync(req, CancellationToken.None);

            res.AccountId.Should().Be(acc.account_id);
            res.Email.Should().Be(acc.email);
            res.Token.Should().Be("jwt-login");
            res.Roles.Should().BeEquivalentTo(roles);

            _repo.VerifyAll();
            _jwt.VerifyAll();
        }

        #endregion

        #region LoginWithGoogleAsync

        // CASE: Token Google invalid -> 401
        [Fact]
        public async Task LoginWithGoogleAsync_Should_Throw_When_Token_Invalid()
        {
            var req = new GoogleLoginRequest { IdToken = "bad-token" };

            _firebase.Setup(f => f.VerifyIdTokenAsync(req.IdToken, It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new Exception("firebase error"));

            var act = () => _svc.LoginWithGoogleAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
             .WithMessage("*Token google không hợp lệ.*");

            _firebase.VerifyAll();
            _repo.VerifyNoOtherCalls();
        }

        // CASE: Account chưa đăng ký -> 409
        [Fact]
        public async Task LoginWithGoogleAsync_Should_Throw_When_Account_NotRegistered()
        {
            var req = new GoogleLoginRequest { IdToken = "ok-token" };
            var user = new FirebaseUserInfo { Email = "g@ex.com", Uid = "uid" };

            _firebase.Setup(f => f.VerifyIdTokenAsync(req.IdToken, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(user);

            _repo.Setup(r => r.FindAccountByIdentifierAsync(user.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((account?)null);

            var act = () => _svc.LoginWithGoogleAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Tài khoản chưa đăng kí, vui lòng dẫn sang đăng kí.*");

            _firebase.VerifyAll();
            _repo.VerifyAll();
        }

        // CASE: Account banned -> 403
        [Fact]
        public async Task LoginWithGoogleAsync_Should_Throw_When_Account_Banned()
        {
            var req = new GoogleLoginRequest { IdToken = "ok-token" };
            var user = new FirebaseUserInfo { Email = "g@ex.com", Uid = "uid" };
            var acc = MakeAccount(Guid.NewGuid(), status: "banned");

            _firebase.Setup(f => f.VerifyIdTokenAsync(req.IdToken, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(user);

            _repo.Setup(r => r.FindAccountByIdentifierAsync(user.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);

            var act = () => _svc.LoginWithGoogleAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Tài khoản bị banned.*");

            _firebase.VerifyAll();
            _repo.VerifyAll();
        }

        // CASE: Happy path
        [Fact]
        public async Task LoginWithGoogleAsync_Should_Return_Token_On_Success()
        {
            var req = new GoogleLoginRequest { IdToken = "ok-token" };
            var user = new FirebaseUserInfo { Email = "g@ex.com", Uid = "uid" };
            var acc = MakeAccount(Guid.NewGuid());
            var roles = new List<string> { "reader" };

            _firebase.Setup(f => f.VerifyIdTokenAsync(req.IdToken, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(user);

            _repo.Setup(r => r.FindAccountByIdentifierAsync(user.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);
            _repo.Setup(r => r.GetRoleCodesOfAccountAsync(acc.account_id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(roles);

            _jwt.Setup(j => j.CreateToken(acc, roles))
                .Returns(new JwtTokenResult
                {
                    Token = "jwt-google",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });

            var res = await _svc.LoginWithGoogleAsync(req, CancellationToken.None);

            res.AccountId.Should().Be(acc.account_id);
            res.Email.Should().Be(acc.email);
            res.Token.Should().Be("jwt-google");
            res.Roles.Should().BeEquivalentTo(roles);

            _firebase.VerifyAll();
            _repo.VerifyAll();
            _jwt.VerifyAll();
        }

        #endregion

        #region CompleteGoogleRegisterAsync

        // CASE: Token invalid -> 401
        [Fact]
        public async Task CompleteGoogleRegisterAsync_Should_Throw_When_Token_Invalid()
        {
            var req = new CompleteGoogleRegisterRequest { IdToken = "bad", Username = "u1", Password = "123456" };

            _firebase.Setup(f => f.VerifyIdTokenAsync(req.IdToken, It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new Exception("firebase"));

            var act = () => _svc.CompleteGoogleRegisterAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Token google không hợp lệ.*");

            _firebase.VerifyAll();
            _repo.VerifyNoOtherCalls();
        }

        // CASE: Username/email trùng -> 409
        [Fact]
        public async Task CompleteGoogleRegisterAsync_Should_Throw_When_Account_Exists()
        {
            var req = new CompleteGoogleRegisterRequest { IdToken = "ok", Username = "u1", Password = "123456" };
            var user = new FirebaseUserInfo { Email = "g@ex.com", Uid = "uid" };

            _firebase.Setup(f => f.VerifyIdTokenAsync(req.IdToken, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(user);

            _repo.Setup(r => r.ExistsByUsernameOrEmailAsync(req.Username, user.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

            var act = () => _svc.CompleteGoogleRegisterAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*Email/Username đã tồn tại.*");

            _firebase.VerifyAll();
            _repo.VerifyAll();
        }

        // CASE: Happy path
        [Fact]
        public async Task CompleteGoogleRegisterAsync_Should_Create_Account_And_Return_Token()
        {
            var req = new CompleteGoogleRegisterRequest { IdToken = "ok", Username = "u1", Password = "123456" };
            var user = new FirebaseUserInfo { Email = "g@ex.com", Uid = "uid", Picture = "pic.png" };

            _firebase.Setup(f => f.VerifyIdTokenAsync(req.IdToken, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(user);

            _repo.Setup(r => r.ExistsByUsernameOrEmailAsync(req.Username, user.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

            var newId = Guid.NewGuid();
            _repo.Setup(r => r.AddAccountAsync(It.IsAny<account>(), It.IsAny<CancellationToken>()))
                 .Callback<account, CancellationToken>((a, _) => a.account_id = newId)
                 .ReturnsAsync((account a, CancellationToken _) => a);

            _repo.Setup(r => r.AddReaderAsync(It.Is<reader>(rd => rd.account_id == newId), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((reader rd, CancellationToken _) => rd);

            var roleId = Guid.NewGuid();
            _repo.Setup(r => r.GetRoleIdByCodeAsync("reader", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(roleId);
            _repo.Setup(r => r.AddAccountRoleAsync(newId, roleId, It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

            _mail.Setup(m => m.SendWelcomeEmailAsync(user.Email, req.Username))
                 .Returns(Task.CompletedTask);

            _jwt.Setup(j => j.CreateToken(It.Is<account>(a => a.account_id == newId),
                                          It.Is<IEnumerable<string>>(r => r.Contains("reader"))))
                .Returns(new JwtTokenResult
                {
                    Token = "jwt-complete",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });

            var res = await _svc.CompleteGoogleRegisterAsync(req, CancellationToken.None);

            res.AccountId.Should().Be(newId);
            res.Email.Should().Be(user.Email);
            res.Token.Should().Be("jwt-complete");
            res.Roles.Should().ContainSingle("reader");

            _firebase.VerifyAll();
            _repo.VerifyAll();
            _mail.VerifyAll();
            _jwt.VerifyAll();
        }

        #endregion

        #region ForgotPassword

        // CASE: SendForgot: account không tồn tại -> return, không làm gì
        [Fact]
        public async Task SendForgotOtpAsync_Should_Do_Nothing_When_Email_NotFound()
        {
            var req = new ForgotPasswordRequest { Email = "x@ex.com" };

            _repo.Setup(r => r.FindAccountByEmailAsync(req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((account?)null);

            await _svc.SendForgotOtpAsync(req, CancellationToken.None);

            _repo.VerifyAll();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: SendForgot: bị rate-limit -> 429
        [Fact]
        public async Task SendForgotOtpAsync_Should_Throw_When_RateLimited()
        {
            var req = new ForgotPasswordRequest { Email = "a@ex.com" };
            var acc = MakeAccount(Guid.NewGuid());

            _repo.Setup(r => r.FindAccountByEmailAsync(req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);
            _otpStore.Setup(s => s.CanSendAsync(req.Email)).ReturnsAsync(false);

            var act = () => _svc.SendForgotOtpAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                 .WithMessage("*OTP request quá số lần cho phép.*");

            _repo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: SendForgot: happy path
        [Fact]
        public async Task SendForgotOtpAsync_Should_Save_Otp_And_Send_Email()
        {
            var req = new ForgotPasswordRequest { Email = "a@ex.com" };
            var acc = MakeAccount(Guid.NewGuid());

            _repo.Setup(r => r.FindAccountByEmailAsync(req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);
            _otpStore.Setup(s => s.CanSendAsync(req.Email)).ReturnsAsync(true);

            _otpStore.Setup(s => s.SaveForgotAsync(req.Email, It.IsAny<string>(), string.Empty))
                     .Returns(Task.CompletedTask);
            _mail.Setup(m => m.SendOtpForgotEmailAsync(req.Email, It.IsAny<string>()))
                 .Returns(Task.CompletedTask);

            await _svc.SendForgotOtpAsync(req, CancellationToken.None);

            _repo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyAll();
        }

        // CASE: VerifyForgot: account không tồn tại -> 404
        [Fact]
        public async Task VerifyForgotAsync_Should_Throw_When_Account_NotFound()
        {
            var req = new VerifyForgotPasswordRequest { Email = "a@ex.com", Otp = "123456", NewPassword = "new123" };

            _repo.Setup(r => r.FindAccountByEmailAsync(req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((account?)null);

            var act = () => _svc.VerifyForgotAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Email không tồn tại.*");

            _repo.VerifyAll();
        }

        // CASE: VerifyForgot: không có entry OTP -> 400
        [Fact]
        public async Task VerifyForgotAsync_Should_Throw_When_Otp_Entry_Missing()
        {
            var req = new VerifyForgotPasswordRequest { Email = "a@ex.com", Otp = "123456", NewPassword = "new123" };
            var acc = MakeAccount(Guid.NewGuid());

            _repo.Setup(r => r.FindAccountByEmailAsync(req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);
            _otpStore.Setup(s => s.GetForgotAsync(req.Email))
                     .ReturnsAsync((ValueTuple<string, string>?)null);

            var act = () => _svc.VerifyForgotAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*OTP hết hạn hoặc không hợp lệ.*");

            _repo.VerifyAll();
            _otpStore.VerifyAll();
        }

        // CASE: VerifyForgot: OTP sai -> 400
        [Fact]
        public async Task VerifyForgotAsync_Should_Throw_When_Otp_Not_Match()
        {
            var req = new VerifyForgotPasswordRequest { Email = "a@ex.com", Otp = "000000", NewPassword = "new123" };
            var acc = MakeAccount(Guid.NewGuid());

            _repo.Setup(r => r.FindAccountByEmailAsync(req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);
            _otpStore.Setup(s => s.GetForgotAsync(req.Email))
                     .ReturnsAsync(("123456", ""));

            var act = () => _svc.VerifyForgotAsync(req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*OTP hết hạn hoặc không hợp lệ.*");

            _repo.VerifyAll();
            _otpStore.VerifyAll();
        }

        // CASE: VerifyForgot: happy path
        [Fact]
        public async Task VerifyForgotAsync_Should_Update_Password_And_Delete_Otp()
        {
            var req = new VerifyForgotPasswordRequest { Email = "a@ex.com", Otp = "123456", NewPassword = "new123" };
            var acc = MakeAccount(Guid.NewGuid());

            _repo.Setup(r => r.FindAccountByEmailAsync(req.Email, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(acc);
            _otpStore.Setup(s => s.GetForgotAsync(req.Email))
                     .ReturnsAsync((req.Otp, ""));

            _repo.Setup(r => r.UpdatePasswordHashAsync(acc.account_id,
                            It.Is<string>(h => !string.IsNullOrEmpty(h) && h != req.NewPassword),
                            It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

            _otpStore.Setup(s => s.DeleteForgotAsync(req.Email))
                     .ReturnsAsync(true);

            await _svc.VerifyForgotAsync(req, CancellationToken.None);

            _repo.VerifyAll();
            _otpStore.VerifyAll();
        }

        #endregion
    }
}

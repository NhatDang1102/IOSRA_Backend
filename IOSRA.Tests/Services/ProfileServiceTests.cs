using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Profile;
using Contract.DTOs.Response.Profile;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;
using Service.Implementations;
using Xunit;

namespace IOSRA.Tests.Services
{
    public class ProfileServiceTests
    {
        // Mock strict để call nào chưa setup sẽ lộ ngay
        private readonly Mock<IProfileRepository> _profileRepo;
        private readonly Mock<IImageUploader> _uploader;
        private readonly Mock<IOtpStore> _otpStore;
        private readonly Mock<IMailSender> _mail;
        private readonly ProfileService _svc;

        public ProfileServiceTests()
        {
            _profileRepo = new Mock<IProfileRepository>(MockBehavior.Strict);
            _uploader = new Mock<IImageUploader>(MockBehavior.Strict);
            _otpStore = new Mock<IOtpStore>(MockBehavior.Strict);
            _mail = new Mock<IMailSender>(MockBehavior.Strict);

            _svc = new ProfileService(_profileRepo.Object, _uploader.Object, _otpStore.Object, _mail.Object);
        }

        // Helper: dựng entity mẫu cho account/reader
        private static account MakeAccount(Guid id, string email = "u@ex.com") =>
            new account { account_id = id, username = "user01", email = email, avatar_url = "av.png" };

        private static reader MakeReader(Guid id, string? gender = "male") =>
            new reader { account_id = id, bio = "hi", gender = gender, birthdate = new DateOnly(2000, 1, 2) };

        // CASE: Get – trả đúng mapping (gender: male -> M)
        [Fact]
        public async Task GetAsync_Should_Return_Mapped_Profile()
        {
            var id = Guid.NewGuid();

            // Arrange: có account + reader trong DB
            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id));
            _profileRepo.Setup(r => r.GetReaderByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeReader(id, "male"));

            // Act
            var res = await _svc.GetAsync(id, CancellationToken.None);

            // Assert: map đủ field, gender rút gọn
            res.Should().BeEquivalentTo(new ProfileResponse
            {
                AccountId = id,
                Username = "user01",
                Email = "u@ex.com",
                AvatarUrl = "av.png",
                Bio = "hi",
                Gender = "M",
                Birthday = new DateOnly(2000, 1, 2)
            });

            _profileRepo.VerifyAll();
            _uploader.VerifyNoOtherCalls();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: Update – validate gender + map DB + gọi Update + refetch
        [Fact]
        public async Task UpdateAsync_Should_Map_Gender_Call_Update_Then_Refetch()
        {
            var id = Guid.NewGuid();
            var req = new ProfileUpdateRequest { Bio = "new bio", Gender = "F", Birthday = new DateOnly(1999, 12, 31) };

            // Arrange: map "F" -> "female" khi update xuống DB
            _profileRepo.Setup(r => r.UpdateReaderProfileAsync(id, "new bio", "female", req.Birthday, It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

            // Sau update service sẽ gọi lại Get để trả bản mới
            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id));
            _profileRepo.Setup(r => r.GetReaderByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeReader(id, "female"));

            // Act
            var res = await _svc.UpdateAsync(id, req, CancellationToken.None);

            // Assert: gender trả về “F”, dữ liệu lấy theo Get()
            res.Gender.Should().Be("F");
            res.Bio.Should().Be("hi");

            _profileRepo.VerifyAll();
            _uploader.VerifyNoOtherCalls();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: Update – gender invalid => 400
        [Fact]
        public async Task UpdateAsync_Should_Throw_When_Gender_Invalid()
        {
            var id = Guid.NewGuid();
            var req = new ProfileUpdateRequest { Gender = "INVALID" };

            // Act
            var act = () => _svc.UpdateAsync(id, req, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Gender phải là M/F/other/unspecified.*");

            _profileRepo.VerifyNoOtherCalls();
            _uploader.VerifyNoOtherCalls();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: UpdateAvatar – tồn tại account + upload + update url
        [Fact]
        public async Task UpdateAvatarAsync_Should_Upload_And_Update_Url()
        {
            var id = Guid.NewGuid();
            var file = Mock.Of<IFormFile>();

            // Arrange: tồn tại account, upload ok -> có url -> update DB
            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id));
            _uploader.Setup(u => u.UploadAvatarAsync(file, $"avatar_{id}", It.IsAny<CancellationToken>()))
                     .ReturnsAsync("https://cdn/av.jpg");
            _profileRepo.Setup(r => r.UpdateAvatarUrlAsync(id, "https://cdn/av.jpg", It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

            // Act
            var url = await _svc.UpdateAvatarAsync(id, file, CancellationToken.None);

            // Assert
            url.Should().Be("https://cdn/av.jpg");

            _profileRepo.VerifyAll();
            _uploader.VerifyAll();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: SendChangeEmailOtp – khác email cũ + chưa tồn tại + qua rate-limit => lưu OTP & gửi mail
        [Fact]
        public async Task SendChangeEmailOtpAsync_Should_Save_And_Send()
        {
            var id = Guid.NewGuid();
            var req = new ChangeEmailRequest { NewEmail = "new@ex.com" };

            // Arrange
            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id, "old@ex.com"));
            _profileRepo.Setup(r => r.ExistsByEmailAsync("new@ex.com", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(false);
            _otpStore.Setup(s => s.CanSendAsync("new@ex.com")).ReturnsAsync(true);
            _otpStore.Setup(s => s.SaveEmailChangeAsync(id, "new@ex.com", It.IsAny<string>()))
                     .Returns(Task.CompletedTask);
            _mail.Setup(m => m.SendChangeEmailOtpAsync("new@ex.com", It.IsAny<string>()))
                 .Returns(Task.CompletedTask);

            // Act
            await _svc.SendChangeEmailOtpAsync(id, req, CancellationToken.None);

            // Assert
            _profileRepo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyAll();
            _uploader.VerifyNoOtherCalls();
        }

        // CASE: VerifyChangeEmail – OTP hợp lệ => update + xóa entry + gửi mail success
        [Fact]
        public async Task VerifyChangeEmailAsync_Should_Update_Email_On_Valid_Otp()
        {
            var id = Guid.NewGuid();
            var req = new VerifyChangeEmailRequest { Otp = "123456" };

            // Arrange: OTP đúng, email mới chưa bị chiếm
            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id, "old@ex.com"));
            _otpStore.Setup(s => s.GetEmailChangeAsync(id))
                     .ReturnsAsync(("new@ex.com", "123456"));
            _profileRepo.Setup(r => r.ExistsByEmailAsync("new@ex.com", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(false);
            _profileRepo.Setup(r => r.UpdateEmailAsync(id, "new@ex.com", It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);
            _otpStore.Setup(s => s.DeleteEmailChangeAsync(id)).ReturnsAsync(true);
            _mail.Setup(m => m.SendChangeEmailSuccessAsync("old@ex.com", "new@ex.com"))
                 .Returns(Task.CompletedTask);

            // Act
            await _svc.VerifyChangeEmailAsync(id, req, CancellationToken.None);

            // Assert
            _profileRepo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyAll();
            _uploader.VerifyNoOtherCalls();
        }

        // CASE: VerifyChangeEmail – OTP sai/không tồn tại
        [Fact]
        public async Task VerifyChangeEmailAsync_Should_Throw_When_Otp_Invalid()
        {
            var id = Guid.NewGuid();
            var req = new VerifyChangeEmailRequest { Otp = "wrong" };

            // Arrange: store có entry nhưng OTP không khớp
            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id));
            _otpStore.Setup(s => s.GetEmailChangeAsync(id))
                     .ReturnsAsync(((string NewEmail, string Otp))("new@ex.com", "123456"));

            // Act
            var act = () => _svc.VerifyChangeEmailAsync(id, req, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*OTP is invalid or expired*");

            _profileRepo.Verify(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
            _otpStore.Verify(r => r.GetEmailChangeAsync(id), Times.Once);
            _profileRepo.VerifyNoOtherCalls();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
            _uploader.VerifyNoOtherCalls();
        }

        // CASE: Get – account không tồn tại
        [Fact]
        public async Task GetAsync_Should_Throw_When_Account_NotFound()
        {
            var id = Guid.NewGuid();

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((account?)null);

            var act = () => _svc.GetAsync(id, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Không tìm thấy tài khoản.*");

            _profileRepo.VerifyAll();
            _profileRepo.VerifyNoOtherCalls();
            _uploader.VerifyNoOtherCalls();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: UpdateAvatar – account không tồn tại
        [Fact]
        public async Task UpdateAvatarAsync_Should_Throw_When_Account_NotFound()
        {
            var id = Guid.NewGuid();
            var file = Mock.Of<IFormFile>();

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync((account?)null);

            var act = () => _svc.UpdateAvatarAsync(id, file, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*Không tìm thấy tài khoản.*");

            _profileRepo.VerifyAll();
            _uploader.VerifyNoOtherCalls();
        }

        // CASE: UpdateAvatar – upload lỗi => không update DB
        [Fact]
        public async Task UpdateAvatarAsync_Should_Bubble_When_Upload_Fails()
        {
            var id = Guid.NewGuid();
            var file = Mock.Of<IFormFile>();

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id));
            _uploader.Setup(u => u.UploadAvatarAsync(file, $"avatar_{id}", It.IsAny<CancellationToken>()))
                     .ThrowsAsync(new InvalidOperationException("cloud down"));

            var act = () => _svc.UpdateAvatarAsync(id, file, CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                     .WithMessage("*cloud down*");

            // Không được gọi Update DB khi upload fail
            _profileRepo.Verify(r => r.UpdateAvatarUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _profileRepo.VerifyAll();
            _uploader.VerifyAll();
        }

        // CASE: SendChangeEmailOtp – email mới trùng email hiện tại
        [Fact]
        public async Task SendChangeEmailOtpAsync_Should_Throw_When_Same_Email()
        {
            var id = Guid.NewGuid();
            var req = new ChangeEmailRequest { NewEmail = "same@ex.com" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id, "same@ex.com"));

            var act = () => _svc.SendChangeEmailOtpAsync(id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("New email must be different from the current email.");

            _profileRepo.VerifyAll();
            _profileRepo.VerifyNoOtherCalls();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: SendChangeEmailOtp – email mới đã tồn tại
        [Fact]
        public async Task SendChangeEmailOtpAsync_Should_Throw_When_New_Email_Taken()
        {
            var id = Guid.NewGuid();
            var req = new ChangeEmailRequest { NewEmail = "new@ex.com" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id, "old@ex.com"));
            _profileRepo.Setup(r => r.ExistsByEmailAsync("new@ex.com", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

            var act = () => _svc.SendChangeEmailOtpAsync(id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*email is already in use*");

            _profileRepo.VerifyAll();
            _otpStore.VerifyNoOtherCalls();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: SendChangeEmailOtp – bị rate-limit
        [Fact]
        public async Task SendChangeEmailOtpAsync_Should_Throw_When_Rate_Limited()
        {
            var id = Guid.NewGuid();
            var req = new ChangeEmailRequest { NewEmail = "new@ex.com" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id, "old@ex.com"));
            _profileRepo.Setup(r => r.ExistsByEmailAsync("new@ex.com", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(false);
            _otpStore.Setup(s => s.CanSendAsync("new@ex.com")).ReturnsAsync(false);

            var act = () => _svc.SendChangeEmailOtpAsync(id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("OTP request rate limit exceeded.");

            _profileRepo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyNoOtherCalls();
        }

        // CASE: VerifyChangeEmail – không có pending entry
        [Fact]
        public async Task VerifyChangeEmailAsync_Should_Throw_When_Pending_NotFound()
        {
            var id = Guid.NewGuid();
            var req = new VerifyChangeEmailRequest { Otp = "123456" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id, "old@ex.com"));

            // Không có entry hoặc entry rỗng
            _otpStore.Setup(s => s.GetEmailChangeAsync(id))
                     .ReturnsAsync(((string NewEmail, string Otp))(null!, null!));

            var act = () => _svc.VerifyChangeEmailAsync(id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*OTP is invalid or expired*");

            _profileRepo.VerifyAll();
            _otpStore.VerifyAll();
            _profileRepo.Verify(r => r.UpdateEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mail.VerifyNoOtherCalls();
        }

        // CASE: VerifyChangeEmail – email mới đã bị chiếm
        [Fact]
        public async Task VerifyChangeEmailAsync_Should_Throw_When_New_Email_Taken()
        {
            var id = Guid.NewGuid();
            var req = new VerifyChangeEmailRequest { Otp = "123456" };

            _profileRepo.Setup(r => r.GetAccountByIdAsync(id, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(MakeAccount(id, "old@ex.com"));
            _otpStore.Setup(s => s.GetEmailChangeAsync(id))
                     .ReturnsAsync(("new@ex.com", "123456"));
            _profileRepo.Setup(r => r.ExistsByEmailAsync("new@ex.com", It.IsAny<CancellationToken>()))
                        .ReturnsAsync(true);

            var act = () => _svc.VerifyChangeEmailAsync(id, req, CancellationToken.None);

            await act.Should().ThrowAsync<AppException>()
                     .WithMessage("*email is already in use*");

            _profileRepo.VerifyAll();
            _otpStore.VerifyAll();
            _mail.VerifyNoOtherCalls();
        }
    }
}

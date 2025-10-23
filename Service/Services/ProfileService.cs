using Contract.DTOs.Request.Profile;
using Contract.DTOs.Respond.Profile;
using Microsoft.AspNetCore.Http;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;

namespace Service.Implementations
{
    public class ProfileService : IProfileService
    {
        private static readonly HashSet<string> AllowedGenderInput = new(StringComparer.OrdinalIgnoreCase)
        { "M", "F", "other", "unspecified" };

        private readonly IProfileRepository _profileRepo;
        private readonly IImageUploader _uploader;
        private readonly IOtpStore _otpStore;
        private readonly IMailSender _mail;

        public ProfileService(IProfileRepository profileRepo, IImageUploader uploader, IOtpStore otpStore, IMailSender mail)
        {
            _profileRepo = profileRepo;
            _uploader = uploader;
            _otpStore = otpStore;
            _mail = mail;
        }

        private static string? ToDbGender(string? input)
        {
            if (input is null) return null;
            return input.ToLowerInvariant() switch
            {
                "m" => "male",
                "f" => "female",
                "other" => "other",
                "unspecified" => "unspecified",
                _ => throw new AppException("ValidationFailed", "Gender chỉ nhận M/F/other/unspecified.", 400)
            };
        }

        private static string FromDbGender(string? db)
        {
            return db?.ToLowerInvariant() switch
            {
                "male" => "M",
                "female" => "F",
                "other" => "other",
                "unspecified" or null => "unspecified",
                _ => "unspecified"
            };
        }

        public async Task<ProfileResponse> GetAsync(ulong accountId, CancellationToken ct = default)
        {
            var acc = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                      ?? throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);

            var reader = await _profileRepo.GetReaderByIdAsync(accountId, ct)
                         ?? throw new AppException("NotFound", "Không tìm thấy hồ sơ reader.", 404);

            return new ProfileResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                AvatarUrl = acc.avatar_url,
                Bio = reader.bio,
                Gender = FromDbGender(reader.gender),
                Birthday = reader.birthdate
            };
        }

        public async Task<ProfileResponse> UpdateAsync(ulong accountId, ProfileUpdateRequest req, CancellationToken ct = default)
        {
            if (req.Gender != null && !AllowedGenderInput.Contains(req.Gender))
                throw new AppException("ValidationFailed", "Gender chỉ nhận M/F/other/unspecified.", 400);

            var dbGender = ToDbGender(req.Gender);
            await _profileRepo.UpdateReaderProfileAsync(accountId, req.Bio, dbGender, req.Birthday, ct);

            return await GetAsync(accountId, ct);
        }

        public async Task<string> UpdateAvatarAsync(ulong accountId, IFormFile file, CancellationToken ct = default)
        {
            var acc = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                      ?? throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);

            var url = await _uploader.UploadAvatarAsync(file, $"avatar_{accountId}", ct);
            await _profileRepo.UpdateAvatarUrlAsync(accountId, url, ct);
            return url;
        }

        // Gửi OTP đổi email (tới email MỚI)
        public async Task SendChangeEmailOtpAsync(ulong accountId, ChangeEmailRequest req, CancellationToken ct = default)
        {
            var acc = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                      ?? throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);

            if (string.Equals(acc.email, req.NewEmail, StringComparison.OrdinalIgnoreCase))
                throw new AppException("ValidationFailed", "Email mới trùng email hiện tại.", 400);

            if (await _profileRepo.ExistsByEmailAsync(req.NewEmail, ct))
                throw new AppException("AccountExists", "Email đã được sử dụng.", 409);

            if (!await _otpStore.CanSendAsync(req.NewEmail))
                throw new AppException("OtpRateLimit", "1 tiếng chỉ đc 1 OTP.", 429);

            var otp = Random.Shared.Next(100000, 1000000).ToString();
            await _otpStore.SaveEmailChangeAsync(accountId, req.NewEmail, otp);

            // ⬇️ dùng mail riêng cho đổi email
            await _mail.SendChangeEmailOtpAsync(req.NewEmail, otp);
        }

        // Xác minh OTP đổi email, cập nhật email, rồi thông báo về email CŨ
        public async Task VerifyChangeEmailAsync(ulong accountId, VerifyChangeEmailRequest req, CancellationToken ct = default)
        {
            var acc = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                      ?? throw new AppException("NotFound", "Không tìm thấy tài khoản.", 404);

            var entry = await _otpStore.GetEmailChangeAsync(accountId);
            if (entry is null) throw new AppException("InvalidOtp", "OTP hết hạn/sai.", 400);

            var (newEmail, otp) = entry.Value;
            if (!string.Equals(req.Otp, otp, StringComparison.Ordinal))
                throw new AppException("InvalidOtp", "OTP hết hạn/sai.", 400);

            if (await _profileRepo.ExistsByEmailAsync(newEmail, ct))
                throw new AppException("AccountExists", "Email đã được sử dụng.", 409);

            var oldEmail = acc.email;

            await _profileRepo.UpdateEmailAsync(accountId, newEmail, ct);
            await _otpStore.DeleteEmailChangeAsync(accountId);

            // ⬇️ gửi thông báo đổi email thành công về email cũ (cảnh báo nếu không phải bạn)
            _ = _mail.SendChangeEmailSuccessAsync(oldEmail, newEmail);
        }
    }
}

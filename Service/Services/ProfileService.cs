using Contract.DTOs.Request.Profile;
using Contract.DTOs.Response.Subscription;
using Microsoft.AspNetCore.Http;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Repository.Utils;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Response.Profile;

namespace Service.Implementations
{
    public class ProfileService : IProfileService
    {
        //các giá trị đc nhận trong field gender
        private static readonly HashSet<string> AllowedGenderInput = new(StringComparer.OrdinalIgnoreCase)
        { "M", "F", "other", "unspecified" };

        private readonly IProfileRepository _profileRepo;
        private readonly IImageUploader _uploader;
        private readonly IOtpStore _otpStore;
        private readonly IMailSender _mail;
        private readonly ISubscriptionService _subscriptionService;

        private static readonly ISubscriptionService NoopSubscriptionService = new NullSubscriptionService();

        public ProfileService(IProfileRepository profileRepo, IImageUploader uploader, IOtpStore otpStore, IMailSender mail, ISubscriptionService? subscriptionService = null)
        {
            _profileRepo = profileRepo;
            _uploader = uploader;
            _otpStore = otpStore;
            _mail = mail;
            _subscriptionService = subscriptionService ?? NoopSubscriptionService;
        }

        private static string? ToDbGender(string? input)
        {
            if (input is null)
            {
                return null;
            }
            //convert input trc khi đẩy sang db
            return input.ToLowerInvariant() switch
            {
                "m" => "male",
                "f" => "female",
                "other" => "other",
                "unspecified" => "unspecified",
                _ => throw new AppException("ValidationFailed", "Gender phải là M/F/other/unspecified.", 400)
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

        public async Task<ProfileResponse> GetAsync(Guid accountId, CancellationToken ct = default)
        {
            //tìm acc trong db
            var acc = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                      ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404);
            //tìm acc xong thì tìm profile reader
            var reader = await _profileRepo.GetReaderByIdAsync(accountId, ct)
                         ?? throw new AppException("ReaderProfileMissing", "Không tìm thấy profile Reader.", 404);

            //check xem acc đó phải author ko
            var author = acc.author;
            AuthorProfileSummary? authorSummary = null;
            //nếu đúng là author -> response gắn thêm profile bảng author
            if (author is not null)
            {
                authorSummary = new AuthorProfileSummary
                {
                    AuthorId = author.account_id,
                    IsRestricted = author.restricted,
                    IsVerified = author.verified_status,
                    TotalFollower = author.total_follower,
                    TotalStory = author.total_story,
                    RankId = author.rank_id,
                    RankName = author.rank?.rank_name,
                    RankRewardRate = author.rank?.reward_rate,
                    RankMinFollowers = author.rank?.min_followers
                };
            }

            return new ProfileResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                AvatarUrl = acc.avatar_url,
                Bio = reader.bio,
                Gender = FromDbGender(reader.gender),
                Birthday = reader.birthdate,
                Strike = acc.strike,
                StrikeStatus = string.IsNullOrWhiteSpace(acc.strike_status) ? "none" : acc.strike_status,
                StrikeRestrictedUntil = acc.strike_restricted_until,
                VoiceCharBalance = acc.voice_wallet?.balance_chars ?? 0,
                IsAuthor = author is not null,
                Author = authorSummary
            };
        }

        public async Task<ProfileWalletResponse> GetWalletAsync(Guid accountId, CancellationToken ct = default)
        {
            //tìm acc trong db account
            var account = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                          ?? throw new AppException("AccountNotFound", "Không tìm thấy tài khoản.", 404);

            var diaWallet = account.dia_wallet;
            var voiceWallet = account.voice_wallet;

            //check coi có phải author ko
            var isAuthor = account.author != null;

            var subscription = await _subscriptionService.GetStatusAsync(accountId, ct);

            return new ProfileWalletResponse
            {
                DiaBalance = diaWallet?.balance_coin ?? 0,
                IsAuthor = isAuthor,
                VoiceCharBalance = isAuthor ? voiceWallet?.balance_chars : null,
                Subscription = subscription
            };
        }

        public async Task<ProfileResponse> UpdateAsync(Guid accountId, ProfileUpdateRequest req, CancellationToken ct = default)
        {
            //check validation gender field
            if (req.Gender != null && !AllowedGenderInput.Contains(req.Gender))
            {
                throw new AppException("ValidationFailed", "Gender phải là M/F/other/unspecified.", 400);
            }
                
            var dbGender = ToDbGender(req.Gender);
            await _profileRepo.UpdateReaderProfileAsync(accountId, req.Bio, dbGender, req.Birthday, ct);

            return await GetAsync(accountId, ct);
        }

        public async Task<string> UpdateAvatarAsync(Guid accountId, IFormFile file, CancellationToken ct = default)
        {
            _ = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            var url = await _uploader.UploadAvatarAsync(file, $"avatar_{accountId}", ct);
            await _profileRepo.UpdateAvatarUrlAsync(accountId, url, ct);
            return url;
        }

        public async Task SendChangeEmailOtpAsync(Guid accountId, ChangeEmailRequest req, CancellationToken ct = default)
        {
            var acc = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                      ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            if (string.Equals(acc.email, req.NewEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException("ValidationFailed", "New email must be different from the current email.", 400);
            }

            if (await _profileRepo.ExistsByEmailAsync(req.NewEmail, ct))
            {
                throw new AppException("AccountExists", "Email is already in use.", 409);
            }

            if (!await _otpStore.CanSendAsync(req.NewEmail))
            {
                throw new AppException("OtpRateLimit", "OTP request rate limit exceeded.", 429);
            }

            var otp = Random.Shared.Next(100000, 1000000).ToString();
            await _otpStore.SaveEmailChangeAsync(accountId, req.NewEmail, otp);
            await _mail.SendChangeEmailOtpAsync(req.NewEmail, otp);
        }

        public async Task VerifyChangeEmailAsync(Guid accountId, VerifyChangeEmailRequest req, CancellationToken ct = default)
        {
            var acc = await _profileRepo.GetAccountByIdAsync(accountId, ct)
                      ?? throw new AppException("AccountNotFound", "Account was not found.", 404);

            var entry = await _otpStore.GetEmailChangeAsync(accountId);
            if (entry is null)
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            var (newEmail, otp) = entry.Value;
            if (!string.Equals(req.Otp, otp, StringComparison.Ordinal))
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            if (await _profileRepo.ExistsByEmailAsync(newEmail, ct))
            {
                throw new AppException("AccountExists", "Email is already in use.", 409);
            }

            var oldEmail = acc.email;

            await _profileRepo.UpdateEmailAsync(accountId, newEmail, ct);
            await _otpStore.DeleteEmailChangeAsync(accountId);

            _ = _mail.SendChangeEmailSuccessAsync(oldEmail, newEmail);
        }

        private sealed class NullSubscriptionService : ISubscriptionService
        {
            public Task<IReadOnlyList<SubscriptionPlanResponse>> GetPlansAsync(CancellationToken ct = default)
                => Task.FromResult<IReadOnlyList<SubscriptionPlanResponse>>(Array.Empty<SubscriptionPlanResponse>());

            public Task<SubscriptionStatusResponse> GetStatusAsync(Guid accountId, CancellationToken ct = default)
                => Task.FromResult(new SubscriptionStatusResponse
                {
                    HasActiveSubscription = false,
                    CanClaimToday = false
                });

            public Task<SubscriptionClaimResponse> ClaimDailyAsync(Guid accountId, CancellationToken ct = default)
                => Task.FromResult(new SubscriptionClaimResponse
                {
                    SubscriptionId = Guid.Empty,
                    ClaimedDias = 0,
                    WalletBalance = 0,
                    ClaimedAt = TimezoneConverter.VietnamNow,
                    NextClaimAvailableAt = null
                });

            public Task ActivateSubscriptionAsync(Guid accountId, string planCode, CancellationToken ct = default)
                => Task.CompletedTask;
        }
    }
}






using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Bc = BCrypt.Net.BCrypt;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Response.Auth;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;
using Service.Models;

namespace Service.Implementations
{

    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepo;
        private readonly IJwtTokenFactory _jwt;
        private readonly IOtpStore _otpStore;
        private readonly IMailSender _mailSender;
        private readonly IFirebaseAuthVerifier _firebase;

        public AuthService(
            IAuthRepository authRepo,
            IJwtTokenFactory jwt,
            IOtpStore otpStore,
            IMailSender mailSender,
            IFirebaseAuthVerifier firebase)
        {
            _authRepo = authRepo;
            _jwt = jwt;
            _otpStore = otpStore;
            _mailSender = mailSender;
            _firebase = firebase;
        }

        public async Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                throw new AppException("InvalidRequest", "Registration request is incomplete.", 400);
            }

            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, req.Email, ct))
            {
                throw new AppException("AccountExists", "Email or username already exists.", 409);
            }

            if (!await _otpStore.CanSendAsync(req.Email))
            {
                throw new AppException("OtpRateLimit", "OTP request rate limit exceeded.", 429);
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var passwordHash = Bc.HashPassword(req.Password);
            await _otpStore.SaveAsync(req.Email, otp, passwordHash, req.Username);
            await _mailSender.SendOtpEmailAsync(req.Email, otp);
        }

        public async Task<LoginResponse> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default)
        {
            var entry = await _otpStore.GetAsync(req.Email);
            if (entry == null)
            {
                throw new AppException("InvalidOtp", "OTP het han/ko hop le.", 400);
            }

            var (otpStored, pwdHashStored, usernameStored) = entry.Value;
            if (!string.Equals(otpStored, req.Otp, StringComparison.Ordinal))
            {
                throw new AppException("InvalidOtp", "OTP het han/ko hop le.", 400);
            }

            var acc = new account
            {
                username = usernameStored,
                email = req.Email,
                password_hash = pwdHashStored,
                status = "unbanned",
                strike = 0
            };

            await _authRepo.AddAccountAsync(acc, ct);
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            await _otpStore.DeleteAsync(req.Email);
            _ = _mailSender.SendWelcomeEmailAsync(req.Email, usernameStored);

            var roles = new List<string> { "reader" };
            var token = _jwt.CreateToken(acc, roles);
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
        {
            var acc = await _authRepo.FindAccountByIdentifierAsync(req.Identifier, ct);
            if (acc == null)
            {
                throw new AppException("AccountNotFound", "Account was not found.", 401);
            }

            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Account has been banned.", 403);
            }

            if (!Bc.Verify(req.Password, acc.password_hash))
            {
                throw new AppException("InvalidCredentials", "Incorrect password.", 401);
            }

            var roles = await _authRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            var token = _jwt.CreateToken(acc, roles);
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default)
        {
            FirebaseUserInfo user;
            try
            {
                user = await _firebase.VerifyIdTokenAsync(req.IdToken, ct);
            }
            catch
            {
                throw new AppException("InvalidGoogleToken", "Google token is invalid or expired.", 401);
            }

            var acc = await _authRepo.FindAccountByIdentifierAsync(user.Email, ct);
            if (acc == null)
            {
                throw new AppException("AccountNotRegistered", "Account is not registered. Please complete sign-up.", 409);
            }

            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Account has been banned.", 403);
            }

            var roles = await _authRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            var token = _jwt.CreateToken(acc, roles);
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default)
        {
            FirebaseUserInfo user;
            try
            {
                user = await _firebase.VerifyIdTokenAsync(req.IdToken, ct);
            }
            catch
            {
                throw new AppException("InvalidGoogleToken", "Google token is invalid or expired.", 401);
            }

            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, user.Email, ct))
            {
                throw new AppException("AccountExists", "Email or username already exists.", 409);
            }

            var pwdHash = Bc.HashPassword(req.Password);
            var acc = new account
            {
                username = req.Username,
                email = user.Email,
                password_hash = pwdHash,
                status = "unbanned",
                strike = 0,
                avatar_url = user.Picture
            };

            await _authRepo.AddAccountAsync(acc, ct);
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            _ = _mailSender.SendWelcomeEmailAsync(user.Email, req.Username);

            var roles = new List<string> { "reader" };
            var token = _jwt.CreateToken(acc, roles);
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task<LoginResponse> RefreshAsync(Guid accountId, CancellationToken ct = default)
        {
            var acc = await _authRepo.FindAccountByIdAsync(accountId, ct);
            if (acc == null)
            {
                throw new AppException("AccountNotFound", "Account was not found.", 404);
            }

            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Account has been banned.", 403);
            }

            var roles = await _authRepo.GetRoleCodesOfAccountAsync(accountId, ct);
            var token = _jwt.CreateToken(acc, roles);
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default)
        {
            var acc = await _authRepo.FindAccountByEmailAsync(req.Email, ct);
            if (acc == null)
            {
                return;
            }

            if (!await _otpStore.CanSendAsync(req.Email))
            {
                throw new AppException("OtpRateLimit", "OTP request rate limit exceeded.", 429);
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            await _otpStore.SaveForgotAsync(req.Email, otp, string.Empty);
            await _mailSender.SendOtpForgotEmailAsync(req.Email, otp);
        }

        public async Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default)
        {
            var acc = await _authRepo.FindAccountByEmailAsync(req.Email, ct)
                      ?? throw new AppException("AccountNotFound", "Email does not exist.", 404);

            var entry = await _otpStore.GetForgotAsync(req.Email);
            if (entry == null)
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            var (otpStored, _) = entry.Value;
            if (!string.Equals(otpStored, req.Otp, StringComparison.Ordinal))
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            var newHash = Bc.HashPassword(req.NewPassword);
            await _authRepo.UpdatePasswordHashAsync(acc.account_id, newHash, ct);
            await _otpStore.DeleteForgotAsync(req.Email);
        }

        private static LoginResponse BuildLoginResponse(account acc, List<string> roles, JwtTokenResult token)
        {
            return new LoginResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                Token = token.Token,
                TokenExpiresAt = token.ExpiresAt,
                Roles = roles
            };
        }
    }
}

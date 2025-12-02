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
            //check khoảng trống
            if (string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                throw new AppException("InvalidRequest", "Chứa khoảng trống.", 400);
            }

            //check trùng
            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, req.Email, ct))
            {
                throw new AppException("AccountExists", "Email/Username đã tồn tại.", 409);
            }

            //check rate limit
            if (!await _otpStore.CanSendAsync(req.Email))
            {
                throw new AppException("OtpRateLimit", "OTP request quá số lần cho phép.", 429);
            }

            //tạo otp 6 chữ số
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            //hash password lại
            var passwordHash = Bc.HashPassword(req.Password);

            //lưu tạm email otp pass user vào redis (chưa đẩy db)
            await _otpStore.SaveAsync(req.Email, otp, passwordHash, req.Username);

            //gửi email otp
            await _mailSender.SendOtpEmailAsync(req.Email, otp);
        }

        public async Task<LoginResponse> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default)
        {
            //lấy otp từ redis
            var entry = await _otpStore.GetAsync(req.Email);
            if (entry == null)
            {
                throw new AppException("InvalidOtp", "OTP hết hạn hoặc không hợp lệ.", 400);
            }

            //so sánh otp
            var (otpStored, pwdHashStored, usernameStored) = entry.Value;
            if (!string.Equals(otpStored, req.Otp, StringComparison.Ordinal))
            {
                throw new AppException("InvalidOtp", "OTP hết hạn hoặc không hợp lệ.", 400);
            }

            //nếu pass 2 cái trên thì tạo entity mới
            var acc = new account
            {
                username = usernameStored,
                email = req.Email,
                password_hash = pwdHashStored,
                status = "unbanned",
                strike = 0
            };

            //save account vô db + tạo profile role reader
            await _authRepo.AddAccountAsync(acc, ct);
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            //xóa otp khỏi redis
            await _otpStore.DeleteAsync(req.Email);
            _ = _mailSender.SendWelcomeEmailAsync(req.Email, usernameStored);

            //tạo jwt token
            var roles = new List<string> { "reader" };
            var token = _jwt.CreateToken(acc, roles);

            //return response (từ dto)
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
        {

            //check account trong db
            var acc = await _authRepo.FindAccountByIdentifierAsync(req.Identifier, ct);

            //valid acc ko tồn tại
            if (acc == null)
            {
                throw new AppException("AccountNotFound", "Tài khoản không tồn tại.", 401);
            }

            //valid acc bị banned
            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Tài khoản bị banned.", 403);
            }


            //valid sai pass
            if (!Bc.Verify(req.Password, acc.password_hash))
            {
                throw new AppException("InvalidCredentials", "Sai pass.", 401);
            }


            //qua đc 3 cái valid trên thì check role
            var roles = await _authRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            //tạo token + response (dto)
            var token = _jwt.CreateToken(acc, roles);
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default)
        {
            FirebaseUserInfo user;
            try
            {
                //verify id token với firebase
                user = await _firebase.VerifyIdTokenAsync(req.IdToken, ct);
            }
            catch
            {
                throw new AppException("InvalidGoogleToken", "Token google không hợp lệ.", 401);
            }

            //tìm acc trong db theo email từ token
            var acc = await _authRepo.FindAccountByIdentifierAsync(user.Email, ct);
            if (acc == null)
            {
                throw new AppException("AccountNotRegistered", "Tài khoản chưa đăng kí, vui lòng dẫn sang đăng kí.", 409);
            }

            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Tài khoản bị banned.", 403);
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
                throw new AppException("InvalidGoogleToken", "Token google không hợp lệ.", 401);
            }

            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, user.Email, ct))
            {
                throw new AppException("AccountExists", "Email/Username đã tồn tại.", 409);
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
                throw new AppException("AccountNotFound", "Tài khoản không tồn tại.", 404);
            }

            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Tài khoản bị banned.", 403);
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
                throw new AppException("OtpRateLimit", "OTP request quá số lần cho phép.", 429);
            }

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            await _otpStore.SaveForgotAsync(req.Email, otp, string.Empty);
            await _mailSender.SendOtpForgotEmailAsync(req.Email, otp);
        }

        public async Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default)
        {
            var acc = await _authRepo.FindAccountByEmailAsync(req.Email, ct)
                      ?? throw new AppException("AccountNotFound", "Email không tồn tại.", 404);

            var entry = await _otpStore.GetForgotAsync(req.Email);
            if (entry == null)
            {
                throw new AppException("InvalidOtp", "OTP hết hạn hoặc không hợp lệ.", 400);
            }

            var (otpStored, _) = entry.Value;
            if (!string.Equals(otpStored, req.Otp, StringComparison.Ordinal))
            {
                throw new AppException("InvalidOtp", "OTP hết hạn hoặc không hợp lệ.", 400);
            }

            var newHash = Bc.HashPassword(req.NewPassword);
            await _authRepo.UpdatePasswordHashAsync(acc.account_id, newHash, ct);
            await _otpStore.DeleteForgotAsync(req.Email);
        }

        private static LoginResponse BuildLoginResponse(account acc, List<string> roles, JwtTokenResult token)
        {
            var vietnamExpires = DateTime.SpecifyKind(token.ExpiresAt, DateTimeKind.Unspecified);

            return new LoginResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                Token = token.Token,
                TokenExpiresAt = vietnamExpires,
                Roles = roles
            };
        }
    }
}

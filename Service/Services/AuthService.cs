using BCrypt.Net;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Respond.Auth;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace Service.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _repo;
        private readonly IJwtTokenFactory _jwt;
        private readonly IOtpStore _otpStore;
        private readonly IMailSender _mail;
        private readonly IFirebaseAuthVerifier _fb;

        public AuthService(
            IAuthRepository repo,
            IJwtTokenFactory jwt,
            IOtpStore otpStore,
            IMailSender mail,
            IFirebaseAuthVerifier fb)
        {
            _repo = repo;
            _jwt = jwt;
            _otpStore = otpStore;
            _mail = mail;
            _fb = fb;
        }

        public async Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                throw new AppException("InvalidRequest", "ko đủ tt đkí.", 400);

            if (!Regex.IsMatch(req.Username, @"^\S+$"))
                throw new AppException("InvalidUsername", "Username không được chứa khoảng trắng.", 400);

            if (await _repo.ExistsByUsernameOrEmailAsync(req.Username, req.Email, ct))
                throw new AppException("AccountExists", "email/username đã có.", 409);

            if (!await _otpStore.CanSendAsync(req.Email))
                throw new AppException("OtpRateLimit", "1 tiếng chỉ đc 1 otp.", 429);

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var bcrypt = BCrypt.Net.BCrypt.HashPassword(req.Password);

            await _otpStore.SaveAsync(req.Email, otp, bcrypt, req.Username);
            await _mail.SendOtpEmailAsync(req.Email, otp);
        }

        public async Task<string> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default)
        {
            var entry = await _otpStore.GetAsync(req.Email);
            if (entry == null) throw new AppException("InvalidOtp", "otp hết hạn/sai.", 400);

            var (otpStored, pwdHashStored, usernameStored) = entry.Value;
            if (otpStored != req.Otp) throw new AppException("InvalidOtp", "otp hết hạn/sai.", 400);

            var acc = new account
            {
                username = usernameStored,
                email = req.Email,
                password_hash = pwdHashStored,
                status = "unbanned",
                strike = 0
            };
            await _repo.AddAccountAsync(acc, ct);
            await _repo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            var readerRoleId = await _repo.GetRoleIdByCodeAsync("reader", ct);
            await _repo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            await _otpStore.DeleteAsync(req.Email);
            _ = _mail.SendWelcomeEmailAsync(req.Email, usernameStored);

            return _jwt.CreateToken(acc, new[] { "reader" });
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
        {
            if (!Regex.IsMatch(req.Identifier, @"^\S+$"))
                throw new AppException("InvalidIdentifier", "Identifier không được chứa khoảng trắng.", 400);

            var acc = await _repo.FindAccountByIdentifierAsync(req.Identifier, ct);
            if (acc == null) throw new AppException("AccountNotFound", "acc ko tồn tại.", 401);
            if (acc.status == "banned") throw new AppException("AccountBanned", "acc bị khóa.", 403);

            var ok = BCrypt.Net.BCrypt.Verify(req.Password, acc.password_hash);
            if (!ok) throw new AppException("InvalidCredentials", "sai pass.", 401);

            var roles = await _repo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            var token = _jwt.CreateToken(acc, roles);

            return new LoginResponse { Username = acc.username, Email = acc.email, Token = token, Roles = roles };
        }

        public async Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default)
        {
            FirebaseUserInfo user;
            try { user = await _fb.VerifyIdTokenAsync(req.IdToken, ct); }
            catch { throw new AppException("InvalidGoogleToken", "Token Google không hợp lệ hoặc hết hạn.", 401); }

            var acc = await _repo.FindAccountByIdentifierAsync(user.Email, ct);
            if (acc == null) throw new AppException("AccountNotRegistered", "chưa đkí tk, hoàn thiện đkí.", 409);
            if (acc.status == "banned") throw new AppException("AccountBanned", "acc đã bị khóa.", 403);

            var roles = await _repo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            var token = _jwt.CreateToken(acc, roles);
            return new LoginResponse { Username = acc.username, Email = acc.email, Token = token, Roles = roles };
        }

        public async Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default)
        {
            FirebaseUserInfo user;
            try { user = await _fb.VerifyIdTokenAsync(req.IdToken, ct); }
            catch { throw new AppException("InvalidGoogleToken", "Token Google không hợp lệ hoặc hết hạn.", 401); }

            if (!Regex.IsMatch(req.Username, @"^\S+$"))
                throw new AppException("InvalidUsername", "Username không được chứa khoảng trắng.", 400);

            if (await _repo.ExistsByUsernameOrEmailAsync(req.Username, user.Email, ct))
                throw new AppException("AccountExists", "email/username đã có tk.", 409);

            if (!Regex.IsMatch(req.Password, @"^(?=.*[A-Za-z])(?=.*\d).{6,20}$"))
                throw new AppException("InvalidPassword", "pass phỉ có 1 chữ 1 số.", 400);

            var pwdHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            var acc = new account
            {
                username = req.Username,
                email = user.Email,
                password_hash = pwdHash,
                status = "unbanned",
                strike = 0,
                avatar_url = user.Picture
            };
            await _repo.AddAccountAsync(acc, ct);
            await _repo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            var readerRoleId = await _repo.GetRoleIdByCodeAsync("reader", ct);
            await _repo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            _ = _mail.SendWelcomeEmailAsync(user.Email, req.Username);

            var token = _jwt.CreateToken(acc, new[] { "reader" });
            return new LoginResponse { Username = acc.username, Email = acc.email, Token = token, Roles = new() { "reader" } };
        }

        // Forgot password: gửi OTP
        public async Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default)
        {
            var acc = await _repo.FindAccountByEmailAsync(req.Email, ct);
            if (acc == null) return; // tránh lộ email tồn tại

            if (!await _otpStore.CanSendAsync(req.Email))
                throw new AppException("OtpRateLimit", "1 tiếng chỉ đc 1 otp.", 429);

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            // Không lưu password mới ở bước này – sẽ lưu khi user submit cùng OTP
            await _otpStore.SaveForgotAsync(req.Email, otp, newPasswordBcrypt: string.Empty);
            await _mail.SendOtpForgotEmailAsync(req.Email, otp);
        }

        // Forgot password: xác minh OTP và đổi mật khẩu
        public async Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default)
        {
            var acc = await _repo.FindAccountByEmailAsync(req.Email, ct);
            if (acc == null) throw new AppException("AccountNotFound", "Email không tồn tại.", 404);

            var entry = await _otpStore.GetForgotAsync(req.Email);
            if (entry == null) throw new AppException("InvalidOtp", "otp hết hạn/sai.", 400);

            var (otpStored, _) = entry.Value;
            if (otpStored != req.Otp) throw new AppException("InvalidOtp", "otp hết hạn/sai.", 400);

            if (!Regex.IsMatch(req.NewPassword, @"^(?=.*[A-Za-z])(?=.*\d).{6,20}$"))
                throw new AppException("InvalidPassword", "Mật khẩu mới không hợp lệ.", 400);

            var newHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _repo.UpdatePasswordHashAsync(acc.account_id, newHash, ct);
            await _otpStore.DeleteForgotAsync(req.Email);
        }
    }
}

using BCrypt.Net;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Respond.Auth;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;
using System.Security.Cryptography;

namespace Service.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepo;
        private readonly IJwtTokenFactory _jwt;
        private readonly IOtpStore _otpStore;
        private readonly IMailSender _mail;
        private readonly IFirebaseAuthVerifier _fb;

        public AuthService(
            IAuthRepository authRepo,
            IJwtTokenFactory jwt,
            IOtpStore otpStore,
            IMailSender mail,
            IFirebaseAuthVerifier fb)
        {
            _authRepo = authRepo;
            _jwt = jwt;
            _otpStore = otpStore;
            _mail = mail;
            _fb = fb;
        }

        public async Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default)
        {
            // Các validate định dạng/độ dài... đã làm ở DTO (ModelState).
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                throw new AppException("InvalidRequest", "ko đủ tt đkí.", 400);

            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, req.Email, ct))
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
            await _authRepo.AddAccountAsync(acc, ct);
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            await _otpStore.DeleteAsync(req.Email);
            _ = _mail.SendWelcomeEmailAsync(req.Email, usernameStored);

            return _jwt.CreateToken(acc, new[] { "reader" });
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
        {
            // Không kiểm tra regex ở Service nữa — đã do DTO đảm nhiệm
            var acc = await _authRepo.FindAccountByIdentifierAsync(req.Identifier, ct);
            if (acc == null) throw new AppException("AccountNotFound", "acc ko tồn tại.", 401);
            if (acc.status == "banned") throw new AppException("AccountBanned", "acc bị khóa.", 403);

            var ok = BCrypt.Net.BCrypt.Verify(req.Password, acc.password_hash);
            if (!ok) throw new AppException("InvalidCredentials", "sai pass.", 401);

            var roles = await _authRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            var token = _jwt.CreateToken(acc, roles);

            return new LoginResponse { Username = acc.username, Email = acc.email, Token = token, Roles = roles };
        }

        public async Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default)
        {
            FirebaseUserInfo user;
            try { user = await _fb.VerifyIdTokenAsync(req.IdToken, ct); }
            catch { throw new AppException("InvalidGoogleToken", "Token Google không hợp lệ hoặc hết hạn.", 401); }

            var acc = await _authRepo.FindAccountByIdentifierAsync(user.Email, ct);
            if (acc == null) throw new AppException("AccountNotRegistered", "chưa đkí tk, hoàn thiện đkí.", 409);
            if (acc.status == "banned") throw new AppException("AccountBanned", "acc đã bị khóa.", 403);

            var roles = await _authRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            var token = _jwt.CreateToken(acc, roles);
            return new LoginResponse { Username = acc.username, Email = acc.email, Token = token, Roles = roles };
        }

        public async Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default)
        {
            FirebaseUserInfo user;
            try { user = await _fb.VerifyIdTokenAsync(req.IdToken, ct); }
            catch { throw new AppException("InvalidGoogleToken", "Token Google không hợp lệ hoặc hết hạn.", 401); }

            // Định dạng Username/Password đã validate ở DTO
            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, user.Email, ct))
                throw new AppException("AccountExists", "email/username đã có tk.", 409);

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
            await _authRepo.AddAccountAsync(acc, ct);
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            _ = _mail.SendWelcomeEmailAsync(user.Email, req.Username);

            var token = _jwt.CreateToken(acc, new[] { "reader" });
            return new LoginResponse { Username = acc.username, Email = acc.email, Token = token, Roles = new() { "reader" } };
        }

        // Forgot password: gửi OTP
        public async Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default)
        {
            var acc = await _authRepo.FindAccountByEmailAsync(req.Email, ct);
            if (acc == null) return; // tránh lộ email tồn tại

            if (!await _otpStore.CanSendAsync(req.Email))
                throw new AppException("OtpRateLimit", "1 tiếng chỉ đc 1 otp.", 429);

            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            await _otpStore.SaveForgotAsync(req.Email, otp, newPasswordBcrypt: string.Empty);
            await _mail.SendOtpForgotEmailAsync(req.Email, otp);
        }

        // Forgot password: xác minh OTP và đổi mật khẩu
        public async Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default)
        {
            var acc = await _authRepo.FindAccountByEmailAsync(req.Email, ct);
            if (acc == null) throw new AppException("AccountNotFound", "Email không tồn tại.", 404);

            var entry = await _otpStore.GetForgotAsync(req.Email);
            if (entry == null) throw new AppException("InvalidOtp", "otp hết hạn/sai.", 400);

            var (otpStored, _) = entry.Value;
            if (otpStored != req.Otp) throw new AppException("InvalidOtp", "otp hết hạn/sai.", 400);

            // Định dạng mật khẩu mới: nên để ở DTO VerifyForgotPasswordRequest
            var newHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _authRepo.UpdatePasswordHashAsync(acc.account_id, newHash, ct);
            await _otpStore.DeleteForgotAsync(req.Email);
        }
    }
}

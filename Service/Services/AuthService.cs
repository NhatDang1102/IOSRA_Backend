using BCrypt.Net;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Respond.Auth;
using Repository.Entities;
using Repository.Interfaces;
using Service.Exceptions;
using Service.Helpers;
using Service.Interfaces;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Service.Implementations
{
    // Service xử lý logic nghiệp vụ cho các chức năng xác thực
    public class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepo; // Repository truy xuất database
        private readonly IJwtTokenFactory _jwt; // Factory tạo JWT token
        private readonly IOtpStore _otpStore; // Store lưu trữ OTP trong Redis
        private readonly IMailSender _mail; // Service gửi email
        private readonly IFirebaseAuthVerifier _fb; // Service xác thực Firebase token

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

        // Gửi mã OTP đăng ký qua email
        public async Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default)
        {
            // Validate dữ liệu đầu vào
            if (string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                throw new AppException("InvalidRequest", "Registration request is incomplete.", 400);
            }

            // Kiểm tra username hoặc email đã tồn tại chưa
            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, req.Email, ct))
            {
                throw new AppException("AccountExists", "Email or username already exists.", 409);
            }

            // Kiểm tra rate limit gửi OTP (tránh spam)
            if (!await _otpStore.CanSendAsync(req.Email))
            {
                throw new AppException("OtpRateLimit", "OTP request rate limit exceeded.", 429);
            }

            // Tạo mã OTP 6 chữ số ngẫu nhiên
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            // Hash password trước khi lưu vào Redis
            var bcrypt = BCrypt.Net.BCrypt.HashPassword(req.Password);

            // Lưu OTP, password hash và username vào Redis với TTL
            await _otpStore.SaveAsync(req.Email, otp, bcrypt, req.Username);
            // Gửi email chứa mã OTP
            await _mail.SendOtpEmailAsync(req.Email, otp);
        }

        // Xác thực OTP và tạo tài khoản mới
        public async Task<LoginResponse> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default)
        {
            // Lấy thông tin OTP đã lưu trong Redis
            var entry = await _otpStore.GetAsync(req.Email);
            if (entry == null)
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            // Giải nén thông tin: OTP, password hash, username
            var (otpStored, pwdHashStored, usernameStored) = entry.Value;
            // Kiểm tra OTP có khớp không
            if (otpStored != req.Otp)
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            // Tạo entity account mới
            var acc = new account
            {
                username = usernameStored,
                email = req.Email,
                password_hash = pwdHashStored,
                status = "unbanned",
                strike = 0
            };
            // Lưu account vào database
            await _authRepo.AddAccountAsync(acc, ct);
            // Tạo bản ghi reader tương ứng
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            // Gán role "reader" cho account mới
            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            // Xóa OTP khỏi Redis sau khi xác thực thành công
            await _otpStore.DeleteAsync(req.Email);
            // Gửi email chào mừng (fire and forget)
            _ = _mail.SendWelcomeEmailAsync(req.Email, usernameStored);

            // Tạo JWT token với role reader
            var roles = new List<string> { "reader" };
            var token = _jwt.CreateToken(acc, roles);

            return new LoginResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                Token = token,
                Roles = roles
            };
        }

        // Đăng nhập bằng email/username và password
        public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
        {
            // Tìm account theo email hoặc username
            var acc = await _authRepo.FindAccountByIdentifierAsync(req.Identifier, ct);
            if (acc == null)
            {
                throw new AppException("AccountNotFound", "Account was not found.", 401);
            }

            // Kiểm tra tài khoản có bị ban không
            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Account has been banned.", 403);
            }

            // Verify password với BCrypt
            var ok = BCrypt.Net.BCrypt.Verify(req.Password, acc.password_hash);
            if (!ok)
            {
                throw new AppException("InvalidCredentials", "Incorrect password.", 401);
            }

            // Lấy danh sách roles của account
            var roles = await _authRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            // Tạo JWT token
            var token = _jwt.CreateToken(acc, roles);

            return new LoginResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                Token = token,
                Roles = roles
            };
        }

        // Đăng nhập bằng Google OAuth
        public async Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default)
        {
            // Xác thực Google ID Token với Firebase
            FirebaseUserInfo user;
            try
            {
                user = await _fb.VerifyIdTokenAsync(req.IdToken, ct);
            }
            catch
            {
                throw new AppException("InvalidGoogleToken", "Google token is invalid or expired.", 401);
            }

            // Tìm account theo email từ Google
            var acc = await _authRepo.FindAccountByIdentifierAsync(user.Email, ct);
            if (acc == null)
            {
                // Nếu chưa có account thì yêu cầu complete registration
                throw new AppException("AccountNotRegistered", "Account is not registered. Please complete sign-up.", 409);
            }

            // Kiểm tra tài khoản có bị ban không
            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Account has been banned.", 403);
            }

            // Lấy roles và tạo JWT token
            var roles = await _authRepo.GetRoleCodesOfAccountAsync(acc.account_id, ct);
            var token = _jwt.CreateToken(acc, roles);
            return new LoginResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                Token = token,
                Roles = roles
            };
        }

        // Hoàn tất đăng ký cho tài khoản Google mới (tạo username + password)
        public async Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default)
        {
            // Xác thực Google ID Token
            FirebaseUserInfo user;
            try
            {
                user = await _fb.VerifyIdTokenAsync(req.IdToken, ct);
            }
            catch
            {
                throw new AppException("InvalidGoogleToken", "Google token is invalid or expired.", 401);
            }

            // Kiểm tra username hoặc email đã tồn tại chưa
            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, user.Email, ct))
            {
                throw new AppException("AccountExists", "Email or username already exists.", 409);
            }

            // Hash password
            var pwdHash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            // Tạo account mới với thông tin từ Google
            var acc = new account
            {
                username = req.Username,
                email = user.Email,
                password_hash = pwdHash,
                status = "unbanned",
                strike = 0,
                avatar_url = user.Picture // Lấy avatar từ Google
            };
            await _authRepo.AddAccountAsync(acc, ct);
            // Tạo bản ghi reader
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            // Gán role "reader"
            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            // Gửi email chào mừng (fire and forget)
            _ = _mail.SendWelcomeEmailAsync(user.Email, req.Username);

            // Tạo JWT token
            var roles = new List<string> { "reader" };
            var token = _jwt.CreateToken(acc, roles);
            return new LoginResponse
            {
                AccountId = acc.account_id,
                Username = acc.username,
                Email = acc.email,
                Token = token,
                Roles = roles
            };
        }

        // Gửi OTP để reset mật khẩu
        public async Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default)
        {
            // Tìm account theo email
            var acc = await _authRepo.FindAccountByEmailAsync(req.Email, ct);
            if (acc == null)
            {
                // Không tìm thấy email nhưng vẫn return thành công (tránh lộ thông tin)
                return;
            }

            // Kiểm tra rate limit
            if (!await _otpStore.CanSendAsync(req.Email))
            {
                throw new AppException("OtpRateLimit", "OTP request rate limit exceeded.", 429);
            }

            // Tạo mã OTP 6 chữ số
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            // Lưu OTP vào Redis (password mới sẽ hash sau khi verify)
            await _otpStore.SaveForgotAsync(req.Email, otp, newPasswordBcrypt: string.Empty);
            // Gửi email chứa OTP
            await _mail.SendOtpForgotEmailAsync(req.Email, otp);
        }

        // Xác thực OTP và cập nhật mật khẩu mới
        public async Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default)
        {
            // Tìm account theo email
            var acc = await _authRepo.FindAccountByEmailAsync(req.Email, ct);
            if (acc == null)
            {
                throw new AppException("AccountNotFound", "Email does not exist.", 404);
            }

            // Lấy OTP từ Redis
            var entry = await _otpStore.GetForgotAsync(req.Email);
            if (entry == null)
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            // Kiểm tra OTP có khớp không
            var (otpStored, _) = entry.Value;
            if (otpStored != req.Otp)
            {
                throw new AppException("InvalidOtp", "OTP is invalid or expired.", 400);
            }

            // Hash password mới và cập nhật vào database
            var newHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            await _authRepo.UpdatePasswordHashAsync(acc.account_id, newHash, ct);
            // Xóa OTP khỏi Redis
            await _otpStore.DeleteForgotAsync(req.Email);
        }
    }
}

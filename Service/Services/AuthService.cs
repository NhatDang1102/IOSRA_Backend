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
            //check khoảng trống (validation cơ bản)
            if (string.IsNullOrWhiteSpace(req.Username) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password))
            {
                throw new AppException("InvalidRequest", "Chứa khoảng trống.", 400);
            }

            //check trùng (Username và Email phải là duy nhất)
            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, req.Email, ct))
            {
                throw new AppException("AccountExists", "Email/Username đã tồn tại.", 409);
            }

            //check rate limit (Redis limiter) để chống spam OTP
            if (!await _otpStore.CanSendAsync(req.Email))
            {
                throw new AppException("OtpRateLimit", "OTP request quá số lần cho phép.", 429);
            }

            //tạo otp 6 chữ số ngẫu nhiên an toàn (Secure Random)
            var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            //hash password lại trước khi lưu tạm vào Redis (Bảo mật: không lưu plain text password dù chỉ là cache)
            var passwordHash = Bc.HashPassword(req.Password);

            //lưu tạm email otp pass user vào redis (chưa đẩy db, TTL 5-10 phút)
            await _otpStore.SaveAsync(req.Email, otp, passwordHash, req.Username);

            //gửi email otp qua SMTP
            await _mailSender.SendOtpEmailAsync(req.Email, otp);
        }

        public async Task<LoginResponse> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default)
        {
            //lấy otp từ redis (kiểm tra xem session đăng ký có tồn tại không)
            var entry = await _otpStore.GetAsync(req.Email);
            if (entry == null)
            {
                throw new AppException("InvalidOtp", "OTP hết hạn hoặc không hợp lệ.", 400);
            }

            //so sánh otp người dùng nhập với otp trong Redis
            var (otpStored, pwdHashStored, usernameStored) = entry.Value;
            if (!string.Equals(otpStored, req.Otp, StringComparison.Ordinal))
            {
                throw new AppException("InvalidOtp", "OTP hết hạn hoặc không hợp lệ.", 400);
            }

            //nếu pass 2 cái trên thì tạo entity mới để lưu vào DB chính thức
            var acc = new account
            {
                username = usernameStored,
                email = req.Email,
                password_hash = pwdHashStored,
                status = "unbanned", // Mặc định active
                strike = 0
            };

            //save account vô db + tạo profile role reader (Mặc định ai đăng ký cũng là Reader)
            await _authRepo.AddAccountAsync(acc, ct);
            await _authRepo.AddReaderAsync(new reader { account_id = acc.account_id }, ct);

            //Gán role reader
            var readerRoleId = await _authRepo.GetRoleIdByCodeAsync("reader", ct);
            await _authRepo.AddAccountRoleAsync(acc.account_id, readerRoleId, ct);

            //xóa otp khỏi redis sau khi verify thành công (cleanup)
            await _otpStore.DeleteAsync(req.Email);
            
            //Gửi email chào mừng (Fire & Forget, không await để user không phải đợi)
            _ = _mailSender.SendWelcomeEmailAsync(req.Email, usernameStored);

            //tạo jwt token để user đăng nhập luôn mà không cần login lại
            var roles = new List<string> { "reader" };
            var token = _jwt.CreateToken(acc, roles);

            //return response (từ dto)
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
        {

            //check account trong db (Hỗ trợ đăng nhập bằng cả Email hoặc Username)
            var acc = await _authRepo.FindAccountByIdentifierAsync(req.Identifier, ct);

            //valid acc ko tồn tại
            if (acc == null)
            {
                throw new AppException("AccountNotFound", "Tài khoản không tồn tại.", 401);
            }

            //valid acc bị banned (Nếu status = banned thì chặn)
            if (acc.status == "banned")
            {
                throw new AppException("AccountBanned", "Tài khoản bị banned.", 403);
            }


            //valid sai pass (So sánh hash bằng BCrypt Verify)
            if (!Bc.Verify(req.Password, acc.password_hash))
            {
                throw new AppException("InvalidCredentials", "Sai pass.", 401);
            }


            //qua đc 3 cái valid trên thì check role để đưa vào Claims của Token
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
                //verify id token với firebase server (đảm bảo token không bị giả mạo)
                user = await _firebase.VerifyIdTokenAsync(req.IdToken, ct);
            }
            catch
            {
                throw new AppException("InvalidGoogleToken", "Token google không hợp lệ.", 401);
            }

            //tìm acc trong db theo email lấy từ token Google
            var acc = await _authRepo.FindAccountByIdentifierAsync(user.Email, ct);
            if (acc == null)
            {
                // Nếu chưa có tài khoản -> Yêu cầu đăng ký bổ sung username (Flow: Google Login -> Register Info -> Complete)
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
            // Flow này chạy khi user login Google lần đầu, hệ thống yêu cầu nhập thêm Username/Pass
            FirebaseUserInfo user;
            try
            {
                // Verify lại lần nữa cho chắc
                user = await _firebase.VerifyIdTokenAsync(req.IdToken, ct);
            }
            catch
            {
                throw new AppException("InvalidGoogleToken", "Token google không hợp lệ.", 401);
            }

            // Check trùng username (email thì chắc chắn là của Google rồi)
            if (await _authRepo.ExistsByUsernameOrEmailAsync(req.Username, user.Email, ct))
            {
                throw new AppException("AccountExists", "Email/Username đã tồn tại.", 409);
            }

            var pwdHash = Bc.HashPassword(req.Password);
            var acc = new account
            {
                username = req.Username,
                email = user.Email,
                password_hash = pwdHash, // Vẫn lưu pass để user có thể login thường nếu muốn
                status = "unbanned",
                strike = 0,
                avatar_url = user.Picture // Lấy avatar từ Google Profile
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
            // Flow refresh token: Cấp lại Access Token mới mà không cần đăng nhập lại
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
            // Tạo token mới (bao gồm cả Refresh Token mới - Rotation)
            var token = _jwt.CreateToken(acc, roles);
            return BuildLoginResponse(acc, roles, token);
        }

        public async Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default)
        {
            // Kiểm tra email tồn tại (Silent fail nếu không có để bảo mật thông tin user)
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
            // Lưu OTP loại Forgot Password (khác namespace với Register OTP)
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

            // Update mật khẩu mới (đã hash)
            var newHash = Bc.HashPassword(req.NewPassword);
            await _authRepo.UpdatePasswordHashAsync(acc.account_id, newHash, ct);
            
            // Xóa OTP sau khi dùng
            await _otpStore.DeleteForgotAsync(req.Email);
        }

        private static LoginResponse BuildLoginResponse(account acc, List<string> roles, JwtTokenResult token)
        {
            // Convert thời gian hết hạn về DateTime không xác định múi giờ để trả về Client
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
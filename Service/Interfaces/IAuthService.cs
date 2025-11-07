using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Respond.Auth;

namespace Service.Interfaces
{
    // Interface định nghĩa các chức năng xác thực
    public interface IAuthService
    {
        // Đăng ký - Bước 1: Gửi OTP qua email
        Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default);

        // Đăng ký - Bước 2: Xác thực OTP và tạo tài khoản, trả về LoginResponse
        Task<LoginResponse> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default);

        // Đăng nhập thông thường bằng email/username + password
        Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);

        // Đăng nhập bằng Google OAuth
        Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default);

        // Hoàn tất đăng ký tài khoản Google mới
        Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default);

        // Quên mật khẩu - Bước 1: Gửi OTP qua email
        Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default);

        // Quên mật khẩu - Bước 2: Xác thực OTP và cập nhật mật khẩu mới
        Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default);
    }
}

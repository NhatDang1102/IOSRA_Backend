using Contract.DTOs.Request.Auth;
using Contract.DTOs.Respond.Auth;

namespace Service.Interfaces;

public interface IAuthService
{
    Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default);
    Task<string> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default); // JWT
    Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default);
    Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default);
    Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default);
    Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default);

}

using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Auth;
using Contract.DTOs.Response.Auth;

namespace Service.Interfaces
{
 
    public interface IAuthService
    {
        Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default);

        Task<LoginResponse> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default);

        Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);

        Task<LoginResponse> LoginWithGoogleAsync(GoogleLoginRequest req, CancellationToken ct = default);

        Task<LoginResponse> CompleteGoogleRegisterAsync(CompleteGoogleRegisterRequest req, CancellationToken ct = default);


        Task<LoginResponse> RefreshAsync(Guid accountId, CancellationToken ct = default);

        Task SendForgotOtpAsync(ForgotPasswordRequest req, CancellationToken ct = default);

        Task VerifyForgotAsync(VerifyForgotPasswordRequest req, CancellationToken ct = default);
    }
}

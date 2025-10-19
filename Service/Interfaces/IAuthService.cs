using Contract.DTOs.Request;
using Contract.DTOs.Respond;

namespace Service.Interfaces;

public interface IAuthService
{
    Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default);
    Task<string> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default); // JWT
    Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);

}

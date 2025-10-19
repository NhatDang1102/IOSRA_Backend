using Contract.DTOs.Request;

namespace Service.Interfaces;

public interface IAuthService
{
    Task SendRegisterOtpAsync(RegisterRequest req, CancellationToken ct = default);
    Task<string> VerifyRegisterAsync(VerifyOtpRequest req, CancellationToken ct = default); // JWT
}

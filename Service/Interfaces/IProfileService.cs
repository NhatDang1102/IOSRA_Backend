using Contract.DTOs.Request.Profile;
using Contract.DTOs.Respond.Profile;
using Microsoft.AspNetCore.Http;

namespace Service.Interfaces
{
    public interface IProfileService
    {
        Task<ProfileResponse> GetAsync(ulong accountId, CancellationToken ct = default);
        Task<ProfileResponse> UpdateAsync(ulong accountId, ProfileUpdateRequest req, CancellationToken ct = default);
        Task<string> UpdateAvatarAsync(ulong accountId, IFormFile file, CancellationToken ct = default);

        Task SendChangeEmailOtpAsync(ulong accountId, ChangeEmailRequest req, CancellationToken ct = default);
        Task VerifyChangeEmailAsync(ulong accountId, VerifyChangeEmailRequest req, CancellationToken ct = default);
    }
}

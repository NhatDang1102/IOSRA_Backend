using System;
using System.Threading;
using System.Threading.Tasks;
using Contract.DTOs.Request.Profile;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Contract.DTOs.Response.Profile;

namespace Service.Interfaces
{
    public interface IProfileService
    {
        Task<ProfileResponse> GetAsync(Guid accountId, CancellationToken ct = default);
        Task<ProfileResponse> UpdateAsync(Guid accountId, ProfileUpdateRequest req, CancellationToken ct = default);
        Task<string> UpdateAvatarAsync(Guid accountId, IFormFile file, CancellationToken ct = default);

        Task SendChangeEmailOtpAsync(Guid accountId, ChangeEmailRequest req, CancellationToken ct = default);
        Task VerifyChangeEmailAsync(Guid accountId, VerifyChangeEmailRequest req, CancellationToken ct = default);

        Task<ProfileWalletResponse> GetWalletAsync(Guid accountId, CancellationToken ct = default);
    }
}

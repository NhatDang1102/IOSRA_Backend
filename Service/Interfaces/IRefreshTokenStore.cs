using System;
using System.Threading;
using System.Threading.Tasks;
using Service.Models;

namespace Service.Interfaces
{
    public interface IRefreshTokenStore
    {
        Task<RefreshTokenIssueResult> IssueAsync(Guid accountId, CancellationToken ct = default);

        Task<RefreshTokenValidationResult?> ValidateAsync(string refreshToken, CancellationToken ct = default);

        Task RevokeAsync(string refreshToken, CancellationToken ct = default);
    }
}

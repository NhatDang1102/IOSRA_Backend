using System;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;
using Contract.DTOs.Internal;

namespace Repository.Interfaces
{
    public interface IProfileRepository
    {
        Task<account?> GetAccountByIdAsync(Guid accountId, CancellationToken ct = default);
        Task<reader?> GetReaderByIdAsync(Guid accountId, CancellationToken ct = default);

        Task UpdateReaderProfileAsync(Guid accountId, string? bio, string? gender, DateOnly? birthday, CancellationToken ct = default);
        Task UpdateAvatarUrlAsync(Guid accountId, string avatarUrl, CancellationToken ct = default);

        Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
        Task UpdateEmailAsync(Guid accountId, string newEmail, CancellationToken ct = default);
        Task UpdateStrikeAsync(Guid accountId, byte strike, string strikeStatus, DateTime? restrictedUntil, CancellationToken ct = default);
        Task ResetStrikeAsync(Guid accountId, CancellationToken ct = default);
    }
}

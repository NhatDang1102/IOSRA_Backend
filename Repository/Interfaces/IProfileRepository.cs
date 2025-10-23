using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Repository.Entities;

namespace Repository.Interfaces
{
    public interface IProfileRepository
    {
        Task<account?> GetAccountByIdAsync(ulong accountId, CancellationToken ct = default);
        Task<reader?> GetReaderByIdAsync(ulong accountId, CancellationToken ct = default);

        Task UpdateReaderProfileAsync(ulong accountId, string? bio, string? gender, DateOnly? birthday, CancellationToken ct = default);
        Task UpdateAvatarUrlAsync(ulong accountId, string avatarUrl, CancellationToken ct = default);

        Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
        Task UpdateEmailAsync(ulong accountId, string newEmail, CancellationToken ct = default);
    }
}
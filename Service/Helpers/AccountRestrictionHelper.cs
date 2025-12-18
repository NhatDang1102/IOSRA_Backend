using System;
using System.Threading;
using System.Threading.Tasks;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Utils;
using Service.Exceptions;

namespace Service.Helpers
{
    public static class AccountRestrictionHelper
    {
        public static async Task EnsureCanPublishAsync(account accountEntity, IProfileRepository? profileRepository, CancellationToken ct)
        {
            if (profileRepository == null)
            {
                return;
            }

            if (!string.Equals(accountEntity.strike_status, "restricted", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var now = TimezoneConverter.VietnamNow;
            if (!accountEntity.strike_restricted_until.HasValue || accountEntity.strike_restricted_until.Value <= now)
            {
                await profileRepository.ResetStrikeAsync(accountEntity.account_id, ct);
                accountEntity.strike = 0;
                accountEntity.strike_status = "none";
                accountEntity.strike_restricted_until = null;
                return;
            }

            throw new AppException("AccountRestricted", $"Tài khoản của bạn bị hạn chế đăng bài cho đến {accountEntity.strike_restricted_until.Value:O}.", 403);
        }
    }
}
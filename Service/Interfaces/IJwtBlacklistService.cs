using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Interfaces
{
    public interface IJwtBlacklistService
    {
        Task BlacklistAsync(string jti, DateTimeOffset expiresAtUtc, CancellationToken ct = default);
        Task<bool> IsBlacklistedAsync(string jti, CancellationToken ct = default);
    }
}
// Main/Common/ClaimsPrincipalExtensions.cs
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Main.Common;

public static class ClaimsPrincipalExtensions
{

    public static ulong GetAccountId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? throw new UnauthorizedAccessException("Thiếu claim sub");

        if (!ulong.TryParse(sub, out var id))
            throw new UnauthorizedAccessException("sub không hợp lệ");

        return id;
    }
}

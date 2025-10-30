// Main/Common/ClaimsPrincipalExtensions.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Main.Common;

public static class ClaimsPrincipalExtensions
{
    public static ulong GetAccountId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? throw new UnauthorizedAccessException("Missing subject claim.");

        if (!ulong.TryParse(sub, out var id))
        {
            throw new UnauthorizedAccessException("Subject claim is not a valid unsigned long.");
        }

        return id;
    }
}

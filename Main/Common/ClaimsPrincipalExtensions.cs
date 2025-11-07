// Extension methods cho ClaimsPrincipal để trích xuất thông tin từ JWT
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Main.Common;

public static class ClaimsPrincipalExtensions
{
    // Extension method lấy AccountId từ JWT claims
    // Tìm trong sub (subject) hoặc NameIdentifier claim
    public static Guid GetAccountId(this ClaimsPrincipal user)
    {
        // Tìm claim "sub" hoặc NameIdentifier từ JWT token
        var sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
               ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? throw new UnauthorizedAccessException("Missing subject claim.");

        // Parse string thành GUID, throw exception nếu không hợp lệ
        if (!Guid.TryParse(sub, out var id))
        {
            throw new UnauthorizedAccessException("Subject claim is not a valid GUID.");
        }

        return id;
    }
}

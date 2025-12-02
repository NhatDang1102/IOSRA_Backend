using System;

namespace Service.Models
{
    public class JwtTokenResult
    {
        public string Token { get; init; } = string.Empty;

        public DateTime ExpiresAt { get; init; }
    }
}

using System;

namespace Service.Models
{
    public class RefreshTokenIssueResult
    {
        public string Token { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
    }

    public class RefreshTokenValidationResult
    {
        public Guid AccountId { get; init; }
        public Guid TokenId { get; init; }
        public DateTime ExpiresAt { get; init; }
    }
}

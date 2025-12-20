using System;

namespace Contract.DTOs.Response.OperationMod
{
    public class AuthorManagementListItemResponse
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public bool Restricted { get; set; }
        public bool VerifiedStatus { get; set; }
        public uint TotalStory { get; set; }
        public uint TotalFollower { get; set; }
        public long RevenueBalance { get; set; }
        public long RevenuePending { get; set; }
        public long RevenueWithdrawn { get; set; }
        public string? RankName { get; set; }
    }
}

using System;

namespace Repository.DataModels
{
    public class RevenuePurchaseLogData
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public long Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; } = string.Empty; // "chapter" or "voice"
    }
}

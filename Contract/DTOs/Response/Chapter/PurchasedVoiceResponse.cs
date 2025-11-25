using System;

namespace Contract.DTOs.Response.Chapter
{
    public class PurchasedVoiceResponse
    {
        public Guid PurchaseVoiceId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public Guid VoiceId { get; set; }
        public string VoiceName { get; set; } = string.Empty;
        public string VoiceCode { get; set; } = string.Empty;
        public int PriceDias { get; set; }
        public string? AudioUrl { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}

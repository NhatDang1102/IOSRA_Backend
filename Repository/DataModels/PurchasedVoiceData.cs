using System;

namespace Repository.DataModels
{
    public class PurchasedVoiceData
    {
        public Guid PurchaseVoiceId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = string.Empty;
        public int ChapterNo { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public Guid VoiceId { get; set; }
        public string VoiceName { get; set; } = string.Empty;
        public string VoiceCode { get; set; } = string.Empty;
        public uint PriceDias { get; set; }
        public string? AudioUrl { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}

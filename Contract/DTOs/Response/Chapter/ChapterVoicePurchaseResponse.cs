using System;

namespace Contract.DTOs.Response.Chapter
{
    public class ChapterVoicePurchaseResponse
    {
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public int TotalPriceDias { get; set; }
        public long WalletBalanceAfter { get; set; }
        public long AuthorShareAmount { get; set; }
        public DateTime PurchasedAt { get; set; }
        public ChapterPurchasedVoiceResponse[] Voices { get; set; } = Array.Empty<ChapterPurchasedVoiceResponse>();
    }

    public class ChapterPurchasedVoiceResponse
    {
        public Guid VoiceId { get; set; }
        public string VoiceName { get; set; } = string.Empty;
        public string VoiceCode { get; set; } = string.Empty;
        public int PriceDias { get; set; }
    }
}

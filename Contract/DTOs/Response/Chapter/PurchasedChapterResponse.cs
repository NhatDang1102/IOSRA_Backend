using System;

namespace Contract.DTOs.Response.Chapter
{
    public class PurchasedChapterResponse
    {
        public Guid PurchaseId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = string.Empty;
        public int ChapterNo { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public int PriceDias { get; set; }
        public DateTime PurchasedAt { get; set; }
        public PurchasedVoiceResponse[] Voices { get; set; } = Array.Empty<PurchasedVoiceResponse>();
    }
}

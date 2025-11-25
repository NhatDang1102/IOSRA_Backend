using System;

namespace Repository.DataModels
{
    public class PurchasedChapterData
    {
        public Guid ChapterPurchaseId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = string.Empty;
        public int ChapterNo { get; set; }
        public string ChapterTitle { get; set; } = string.Empty;
        public uint PriceDias { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}

using System;

namespace Contract.DTOs.Response.Chapter
{
    public class ChapterPurchaseResponse
    {
        public Guid PurchaseId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid StoryId { get; set; }
        public int ChapterNo { get; set; }
        public string ChapterTitle { get; set; } = null!;
        public int PriceDias { get; set; }
        public long WalletBalanceAfter { get; set; }
        public long AuthorShareVnd { get; set; }
        public DateTime PurchasedAt { get; set; }
    }
}

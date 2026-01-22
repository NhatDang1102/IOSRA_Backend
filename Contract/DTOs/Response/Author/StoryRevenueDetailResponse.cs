using System;
using System.Collections.Generic;

namespace Contract.DTOs.Response.Author
{
    public class ChapterRevenueDetailDto
    {
        public Guid ChapterId { get; set; }
        public uint ChapterNo { get; set; }
        public string Title { get; set; } = string.Empty;
        
        // Stats
        public long TotalRevenue { get; set; }
        public long ChapterRevenue { get; set; }
        public long VoiceRevenue { get; set; }
        
        public int TotalPurchases { get; set; }
        public int TotalPurchaseCount => TotalPurchases;
        public int TotalChapterPurchaseCount { get; set; }
        public int TotalVoicePurchaseCount { get; set; }

        public List<PurchaserDetailDto> Purchasers { get; set; } = new();
    }

    public class StoryRevenueDetailResponse
    {
        public Guid ContentId { get; set; } // StoryId
        public string Title { get; set; } = string.Empty;
        
        // Story Totals
        public long TotalRevenue { get; set; }
        public long ChapterRevenue { get; set; }
        public long VoiceRevenue { get; set; }
        
        public int TotalPurchases { get; set; }
        public int TotalPurchaseCount => TotalPurchases;
        public int TotalChapterPurchaseCount { get; set; }
        public int TotalVoicePurchaseCount { get; set; }

        public List<ChapterRevenueDetailDto> Chapters { get; set; } = new();
    }
}

using System;
using System.Collections.Generic;
using Contract.DTOs.Response.Common;

namespace Contract.DTOs.Response.Author
{
    public class PurchaserDetailDto
    {
        public Guid AccountId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public int Price { get; set; }
        public DateTime PurchaseDate { get; set; }
    }

    public class ContentRevenueDetailResponse
    {
        public Guid ContentId { get; set; } // StoryId or ChapterId
        public string Title { get; set; } = string.Empty;
        public long TotalRevenue { get; set; }
        public int TotalPurchases { get; set; }
        public PagedResult<PurchaserDetailDto> Purchasers { get; set; } = null!;
    }
}

using System;

namespace Contract.DTOs.Response.Author
{
    public class AuthorRevenueSummaryResponse
    {
        public long RevenueBalance { get; set; }
        public long RevenuePending { get; set; }
        public long RevenueWithdrawn { get; set; }
        public long TotalRevenue { get; set; }
        public string? RankName { get; set; }
        public decimal? RankRewardRate { get; set; }
    }
}

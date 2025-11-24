using System;

namespace Contract.DTOs.Respond.Author
{
    public class AuthorRevenueSummaryResponse
    {
        public long RevenueBalanceVnd { get; set; }
        public long RevenuePendingVnd { get; set; }
        public long RevenueWithdrawnVnd { get; set; }
        public long TotalRevenueVnd { get; set; }
        public string? RankName { get; set; }
        public decimal? RankRewardRate { get; set; }
    }
}

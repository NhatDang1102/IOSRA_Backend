namespace Contract.DTOs.Response.Author
{
    public class AuthorRankStatusResponse
    {
        public string CurrentRankName { get; set; } = string.Empty;
        public decimal? CurrentRewardRate { get; set; }
        public int TotalFollowers { get; set; }
        public string? NextRankName { get; set; }
        public decimal? NextRankRewardRate { get; set; }
        public int? NextRankMinFollowers { get; set; }
    }
}

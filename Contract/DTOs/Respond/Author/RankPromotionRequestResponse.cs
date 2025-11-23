using System;

namespace Contract.DTOs.Respond.Author
{
    public class RankPromotionRequestResponse
    {
        public Guid RequestId { get; set; }
        public Guid AuthorId { get; set; }
        public string AuthorUsername { get; set; } = string.Empty;
        public string AuthorEmail { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Commitment { get; set; }
        public string? CurrentRankName { get; set; }
        public string TargetRankName { get; set; } = string.Empty;
        public uint TargetRankMinFollowers { get; set; }
        public uint TotalFollowers { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? ModeratorId { get; set; }
        public string? ModeratorUsername { get; set; }
        public string? ModeratorNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}
